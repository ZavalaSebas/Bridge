namespace Bridge.Core.Entities;

public abstract class DatabaseObject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
}
