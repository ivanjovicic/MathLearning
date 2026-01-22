namespace MathLearning.Domain.Entities;

public class Topic
{
    public int Id { get; private set; }
    public string Name { get; private set; } = "";
    public string? Description { get; private set; }

    private Topic() { }

    public Topic(string name, string? description = null)
    {
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Name is required") : name;
        Description = description;
    }

    public void Rename(string name)
    {
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Name is required") : name;
    }

    public void SetDescription(string? description)
    {
        Description = description;
    }
}
