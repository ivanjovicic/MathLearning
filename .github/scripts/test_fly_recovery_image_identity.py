from __future__ import annotations

import hashlib
import io
import json
import sys
import tarfile
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from fly_recovery_image_identity import verify_archive, verify_loaded_image


SOURCE_SHA = "d945f51446e4d6e853bbc136134915b4c183aaf4"
MANIFEST_DIGEST = "sha256:" + "1" * 64
REVISION_LABEL = "org.opencontainers.image.revision"


def make_config(source_sha: str = SOURCE_SHA) -> bytes:
    return json.dumps(
        {
            "architecture": "amd64",
            "os": "linux",
            "config": {
                "Entrypoint": ["dotnet", "MathLearning.Api.dll"],
                "Labels": {REVISION_LABEL: source_sha},
            },
        },
        separators=(",", ":"),
    ).encode("utf-8")


def make_metadata(config_bytes: bytes) -> dict[str, object]:
    config_digest = "sha256:" + hashlib.sha256(config_bytes).hexdigest()
    return {
        "containerimage.digest": MANIFEST_DIGEST,
        "containerimage.config.digest": config_digest,
        "containerimage.descriptor": {
            "digest": MANIFEST_DIGEST,
            "annotations": {"config.digest": config_digest},
        },
    }


def make_inspection(image_id: str, descriptor: dict[str, str] | None) -> dict[str, object]:
    return {
        "Id": image_id,
        "Descriptor": descriptor,
        "Config": {
            "Entrypoint": ["dotnet", "MathLearning.Api.dll"],
            "Labels": {REVISION_LABEL: SOURCE_SHA},
        },
        "Os": "linux",
        "Architecture": "amd64",
    }


class FlyRecoveryImageIdentityTests(unittest.TestCase):
    def test_containerd_store_image_id_matches_buildx_manifest_descriptor(self) -> None:
        config_digest = make_metadata(make_config())["containerimage.config.digest"]
        lines = verify_loaded_image(
            make_metadata(make_config()),
            make_inspection(
                MANIFEST_DIGEST, {"digest": MANIFEST_DIGEST}
            ),
            SOURCE_SHA,
        )
        self.assertIn(f"Buildx config digest: {config_digest}", lines)
        self.assertIn(MANIFEST_DIGEST, lines[2])
        self.assertIn("containerd image store target descriptor", lines[2])

    def test_classic_store_image_id_matches_buildx_config_digest(self) -> None:
        metadata = make_metadata(make_config())
        lines = verify_loaded_image(
            metadata,
            make_inspection(metadata["containerimage.config.digest"], None),
            SOURCE_SHA,
        )
        self.assertIn("classic image store", lines[2])

    def test_unrelated_loaded_id_is_rejected(self) -> None:
        metadata = make_metadata(make_config())
        with self.assertRaisesRegex(ValueError, "identity mismatch"):
            verify_loaded_image(
                metadata,
                make_inspection("sha256:" + "3" * 64, None),
                SOURCE_SHA,
            )

    def test_containerd_descriptor_must_match_buildx_manifest(self) -> None:
        with self.assertRaisesRegex(ValueError, "identity mismatch"):
            verify_loaded_image(
                make_metadata(make_config()),
                make_inspection(
                    "sha256:" + "2" * 64,
                    {"digest": "sha256:" + "2" * 64},
                ),
                SOURCE_SHA,
            )

    def test_revision_and_entrypoint_are_checked(self) -> None:
        bad_inspection = make_inspection(
            make_metadata(make_config())["containerimage.config.digest"], None
        )
        bad_inspection["Config"]["Labels"][REVISION_LABEL] = "0" * 40
        with self.assertRaisesRegex(ValueError, "revision label"):
            verify_loaded_image(make_metadata(make_config()), bad_inspection, SOURCE_SHA)

        bad_inspection = make_inspection(
            make_metadata(make_config())["containerimage.config.digest"], None
        )
        bad_inspection["Config"]["Entrypoint"] = ["sh"]
        with self.assertRaisesRegex(ValueError, "entrypoint"):
            verify_loaded_image(make_metadata(make_config()), bad_inspection, SOURCE_SHA)

    def test_archive_config_digest_and_contents_are_verified(self) -> None:
        config_bytes = make_config()
        metadata = make_metadata(config_bytes)
        with tempfile.TemporaryDirectory() as temp_dir:
            archive_path = Path(temp_dir) / "image.tar"
            with tarfile.open(archive_path, mode="w") as archive:
                manifest = json.dumps(
                    [{"Config": "image.json", "RepoTags": ["mathlearning-api:recovery-verify"]}]
                ).encode("utf-8")
                for name, payload in (("manifest.json", manifest), ("image.json", config_bytes)):
                    info = tarfile.TarInfo(name)
                    info.size = len(payload)
                    archive.addfile(info, io.BytesIO(payload))

            lines = verify_archive(
                metadata,
                archive_path,
                "mathlearning-api:recovery-verify",
                SOURCE_SHA,
            )
            self.assertIn("matches Buildx config digest", lines[0])
            self.assertEqual(
                f"Exported archive SHA-256: {hashlib.sha256(archive_path.read_bytes()).hexdigest()}",
                lines[1],
            )

    def test_archive_with_changed_config_is_rejected(self) -> None:
        metadata = make_metadata(make_config())
        changed = make_config().replace(b"MathLearning.Api.dll", b"changed.Api.dll")
        with tempfile.TemporaryDirectory() as temp_dir:
            archive_path = Path(temp_dir) / "image.tar"
            with tarfile.open(archive_path, mode="w") as archive:
                manifest = json.dumps(
                    [{"Config": "image.json", "RepoTags": ["mathlearning-api:recovery-verify"]}]
                ).encode("utf-8")
                for name, payload in (("manifest.json", manifest), ("image.json", changed)):
                    info = tarfile.TarInfo(name)
                    info.size = len(payload)
                    archive.addfile(info, io.BytesIO(payload))

            with self.assertRaisesRegex(ValueError, "archive config digest mismatch"):
                verify_archive(
                    metadata,
                    archive_path,
                    "mathlearning-api:recovery-verify",
                    SOURCE_SHA,
                )


if __name__ == "__main__":
    unittest.main()
