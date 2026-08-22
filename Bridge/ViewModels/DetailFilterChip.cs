namespace Bridge.ViewModels;

public sealed class DetailFilterChip(string category, string value, string label)
{
    public string Category { get; } = category;
    public string Value { get; } = value;
    public string Label { get; } = label;
}
