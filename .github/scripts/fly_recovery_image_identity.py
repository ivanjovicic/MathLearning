#!/usr/bin/env python3
"""Verify Buildx, Docker image-store, and exported archive identities."""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import subprocess
import tarfile
from pathlib import Path, PurePosixPath
from typing import Any


DIGEST_RE = re.compile(r"sha256:[0-9a-f]{64}\Z")
SOURCE_SHA_RE = re.compile(r"[0-9a-f]{40}\Z")
REVISION_LABEL = "org.opencontainers.image.revision"
EXPECTED_ENTRYPOINT = ["dotnet", "MathLearning.Api.dll"]


def _read_json(path: Path) -> Any:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise ValueError(f"Could not read JSON from {path}: {exc}") from exc


def _require_digest(value: Any, label: str) -> str:
    if not isinstance(value, str) or not DIGEST_RE.fullmatch(value):
        raise ValueError(f"Missing or invalid {label} digest")
    return value


def _metadata_digests(metadata: dict[str, Any]) -> tuple[str, str]:
    manifest_digest = _require_digest(
        metadata.get("containerimage.digest"), "Buildx manifest"
    )
    config_digest = _require_digest(
        metadata.get("containerimage.config.digest"), "Buildx config"
    )

    descriptor = metadata.get("containerimage.descriptor")
    if descriptor is not None:
        if not isinstance(descriptor, dict):
            raise ValueError("Buildx image descriptor is not an object")
        descriptor_digest = _require_digest(
            descriptor.get("digest"), "Buildx descriptor"
        )
        if descriptor_digest != manifest_digest:
            raise ValueError("Buildx descriptor does not match its manifest digest")
        annotations = descriptor.get("annotations") or {}
        if not isinstance(annotations, dict):
            raise ValueError("Buildx descriptor annotations are not an object")
        annotated_config = annotations.get("config.digest")
        if annotated_config is not None and annotated_config != config_digest:
            raise ValueError("Buildx descriptor config digest does not match metadata")

    return manifest_digest, config_digest


def _validate_source_sha(source_sha: str) -> None:
    if not SOURCE_SHA_RE.fullmatch(source_sha):
        raise ValueError("Expected source SHA must be 40 lowercase hexadecimal characters")


def _verify_image_config(config: dict[str, Any], source_sha: str, source: str) -> None:
    image_config = config.get("config") or config.get("Config") or {}
    labels = image_config.get("Labels") or {}
    if labels.get(REVISION_LABEL) != source_sha:
        raise ValueError(f"{source} OCI revision label does not match approved source")
    if image_config.get("Entrypoint") != EXPECTED_ENTRYPOINT:
        raise ValueError(f"{source} API entrypoint does not match expected value")
    image_os = config.get("os", config.get("Os"))
    architecture = config.get("architecture", config.get("Architecture"))
    if image_os != "linux" or architecture != "amd64":
        raise ValueError(f"{source} image platform is not linux/amd64")


def verify_loaded_image(
    metadata: dict[str, Any], inspection: dict[str, Any], source_sha: str
) -> list[str]:
    _validate_source_sha(source_sha)
    manifest_digest, config_digest = _metadata_digests(metadata)
    image_id = _require_digest(inspection.get("Id"), "loaded Docker image ID")
    descriptor = inspection.get("Descriptor")

    if descriptor is not None:
        if not isinstance(descriptor, dict):
            raise ValueError("Docker image descriptor is not an object")
        target_digest = _require_digest(
            descriptor.get("digest"), "Docker image target descriptor"
        )
        if target_digest != manifest_digest or image_id != target_digest:
            raise ValueError(
                "Loaded Docker image identity mismatch: "
                f"image ID={image_id}, target descriptor={target_digest}, "
                f"Buildx manifest digest={manifest_digest}"
            )
        id_relation = "Buildx manifest digest (containerd image store target descriptor)"
    else:
        if image_id != config_digest:
            raise ValueError(
                "Loaded Docker image identity mismatch: "
                f"image ID={image_id}, Buildx config digest={config_digest} "
                "(classic image store)"
            )
        id_relation = "Buildx config digest (classic image store)"

    _verify_image_config(inspection, source_sha, "Loaded Docker image")
    if inspection.get("Os") != "linux" or inspection.get("Architecture") != "amd64":
        raise ValueError("Loaded Docker image platform is not linux/amd64")

    return [
        f"Buildx local manifest digest (not registry-verified): {manifest_digest}",
        f"Buildx config digest: {config_digest}",
        f"Loaded Docker image ID: {image_id} (matches {id_relation})",
        "Registry digest: none (image was not pushed)",
    ]


def inspect_local_image(image_name: str) -> dict[str, Any]:
    raw = subprocess.check_output(
        ["docker", "image", "inspect", image_name], text=True
    )
    result = json.loads(raw)
    if not isinstance(result, list) or len(result) != 1 or not isinstance(result[0], dict):
        raise ValueError("Docker image inspect did not return exactly one image")
    return result[0]


def _archive_config_digest(archive_path: Path) -> tuple[str, dict[str, Any]]:
    try:
        with tarfile.open(archive_path, mode="r:*") as archive:
            manifest_member = archive.getmember("manifest.json")
            manifest_file = archive.extractfile(manifest_member)
            if manifest_file is None:
                raise ValueError("Docker archive has no readable manifest.json")
            manifest = json.load(manifest_file)
            if not isinstance(manifest, list) or len(manifest) != 1:
                raise ValueError("Docker archive must contain exactly one image")
            record = manifest[0]
            if not isinstance(record, dict):
                raise ValueError("Docker archive image manifest is invalid")
            config_path = record.get("Config")
            if not isinstance(config_path, str):
                raise ValueError("Docker archive manifest has no config path")
            config_posix = PurePosixPath(config_path)
            if (
                config_posix.is_absolute()
                or len(config_posix.parts) != 1
                or config_posix.parts[0] in {"", ".", ".."}
                or "\\" in config_path
            ):
                raise ValueError("Docker archive config path is not a safe root entry")
            config_member = archive.getmember(config_path)
            config_file = archive.extractfile(config_member)
            if config_file is None:
                raise ValueError("Docker archive has no readable image config")
            config_bytes = config_file.read()
            config = json.loads(config_bytes)
            digest = "sha256:" + hashlib.sha256(config_bytes).hexdigest()
            return digest, config
    except (OSError, tarfile.TarError, KeyError, json.JSONDecodeError) as exc:
        raise ValueError(f"Could not inspect Docker image archive: {exc}") from exc


def _file_sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as image_file:
        for chunk in iter(lambda: image_file.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def verify_archive(
    metadata: dict[str, Any], archive_path: Path, image_name: str, source_sha: str
) -> list[str]:
    _validate_source_sha(source_sha)
    _, config_digest = _metadata_digests(metadata)
    archive_config_digest, config = _archive_config_digest(archive_path)
    if archive_config_digest != config_digest:
        raise ValueError(
            "Exported archive config digest mismatch: "
            f"archive={archive_config_digest}, Buildx config={config_digest}"
        )
    with tarfile.open(archive_path, mode="r:*") as archive:
        manifest_file = archive.extractfile(archive.getmember("manifest.json"))
        if manifest_file is None:
            raise ValueError("Docker archive has no readable manifest.json")
        record = json.load(manifest_file)[0]
    repo_tags = record.get("RepoTags") or []
    if image_name not in repo_tags:
        raise ValueError("Exported archive does not contain the expected image tag")
    _verify_image_config(config, source_sha, "Exported archive")
    archive_sha = _file_sha256(archive_path)
    return [
        f"Exported archive config digest: {archive_config_digest} (matches Buildx config digest)",
        f"Exported archive SHA-256: {archive_sha}",
    ]


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)

    loaded = subparsers.add_parser("loaded", help="verify the loaded local image")
    loaded.add_argument("--metadata-file", type=Path, required=True)
    loaded.add_argument("--image-name", required=True)
    loaded.add_argument("--source-sha", required=True)

    archive = subparsers.add_parser("archive", help="verify the exported Docker archive")
    archive.add_argument("--metadata-file", type=Path, required=True)
    archive.add_argument("--archive", type=Path, required=True)
    archive.add_argument("--image-name", required=True)
    archive.add_argument("--source-sha", required=True)

    args = parser.parse_args()
    try:
        metadata = _read_json(args.metadata_file)
        if not isinstance(metadata, dict):
            raise ValueError("Buildx metadata must be a JSON object")
        if args.command == "loaded":
            lines = verify_loaded_image(
                metadata, inspect_local_image(args.image_name), args.source_sha
            )
        else:
            lines = verify_archive(
                metadata, args.archive, args.image_name, args.source_sha
            )
    except (OSError, subprocess.SubprocessError, json.JSONDecodeError, ValueError) as exc:
        parser.exit(1, f"image identity verification failed: {exc}\n")

    print("\n".join(lines))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
