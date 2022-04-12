namespace TwitchAzureTTS;

internal class Color
{
    internal readonly int Id;
    internal readonly string Name;
    internal readonly int Rgb;

    internal Color(int id, string name, int rgb)
    {
        Id = id;
        Name = name;
        Rgb = rgb;
    }

    public override bool Equals(object? obj)
        => obj is not null && obj is Color color && color.Id == Id && color.Name == Name && color.Rgb == Rgb;

    public override int GetHashCode()
        => Id.GetHashCode() ^ Name.GetHashCode() ^ Rgb.GetHashCode();
}
