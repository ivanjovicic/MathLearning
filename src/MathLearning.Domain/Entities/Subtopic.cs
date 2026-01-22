namespace MathLearning.Domain.Entities;

public class Subtopic
{
    public int Id { get; private set; }
    public string Name { get; private set; } = "";
    public int TopicId { get; private set; }
    public Topic? Topic { get; private set; }

    private Subtopic() { }

    public Subtopic(string name, int topicId)
    {
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Name is required") : name;
        TopicId = topicId;
    }

    public void Rename(string name)
    {
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Name is required") : name;
    }

    public void SetTopic(int topicId)
    {
        TopicId = topicId;
    }
}
