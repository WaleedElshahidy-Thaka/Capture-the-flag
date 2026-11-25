using System;
using System.Text;

// Generates the human-shareable room code: 5 characters, alphanumeric, excluding visually
// ambiguous characters (0/O, 1/I/l) — per the reference doc's Code Generation section.
// ~32 million combinations (32^5).
public static class RoomCodeGenerator
{
    const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
    const int Length = 5;

    static readonly Random random = new Random();

    public static string Generate()
    {
        var sb = new StringBuilder(Length);
        for (int i = 0; i < Length; i++)
            sb.Append(Alphabet[random.Next(Alphabet.Length)]);
        return sb.ToString();
    }
}
