namespace MathLearning.Domain.Explanations;

public sealed class MathReasoningGraph
{
    public MathReasoningNode RootNode { get; }
    public IReadOnlyList<MathReasoningNode> Nodes { get; }

    public MathReasoningGraph(MathReasoningNode rootNode, IReadOnlyList<MathReasoningNode> nodes)
    {
        RootNode = rootNode ?? throw new ArgumentNullException(nameof(rootNode));
        Nodes = nodes is { Count: > 0 } ? nodes : throw new ArgumentException("Graph must contain at least one node.", nameof(nodes));
    }
}
