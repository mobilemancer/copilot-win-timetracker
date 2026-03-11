namespace TimeTracker.Domain;

public sealed class Customer
{
    public string Name { get; set; } = string.Empty;

    public bool IsInternal { get; set; }
}
