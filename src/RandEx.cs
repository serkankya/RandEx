﻿using System;
using System.Collections.Generic;
using System.Threading;
using RandEx.Utils;
using static RandEx.Utils.RandomStringCharacter;
using static RandEx.Internal.RandomGenerator;

namespace RandEx;

public static class RandomEx
{
    internal static readonly ThreadLocal<ulong> _threadState = new(GenerateSeed);
    internal static readonly ThreadLocal<(bool HasSpare, double Spare)> _gaussianState = new(() => (false, 0.0), true);
    internal static readonly ThreadLocal<ulong> _sequence = new(GenerateSeed);

    /// <summary>
    /// Sets a custom seed for random number generation.
    /// </summary>
    /// <param name="seed">The seed to set for the random number generator.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if seed is not a positive non-zero value</exception>
    public static void SetSeed(int seed)
    {
        if (seed <= 0)
        {
            throw new ArgumentException("Seed must be a positive non-zero value.", nameof(seed));
        }

        ulong newSeed = (ulong)seed * Multiplier;
        _threadState.Value = newSeed;
        _sequence.Value = GenerateSeed();
    }

    /// <summary>
    /// Generates a random byte (0-255).
    /// </summary>
    /// <returns>A random byte value between 0 and 255.</returns>
    public static byte GetRandomByte() => (byte)GetRandomInt(maxValue: 256);

    /// <summary>
    /// Generates a random boolean value (true or false).
    /// </summary>
    /// <returns>A random boolean value: true or false.</returns>
    public static bool GetRandomBool() => (GenerateRandom() & 1) != 0;

    /// <summary>
    /// Generates a random Guid.
    /// </summary>
    /// <returns>A random Guid.</returns>
    public static Guid GetRandomGuid() => new(GetRandomBytes(16));

    /// <summary>
    /// Generates a random color in the form of a byte tuple (R, G, B).
    /// </summary>
    /// <returns>A random color represented as a byte tuple (R, G, B).</returns>
    public static (byte R, byte G, byte B) GetRandomByteColor() => (GetRandomByte(), GetRandomByte(), GetRandomByte());

    /// <summary>
    /// Generates a random integer between the specified min and max values (exclusive).
    /// </summary>
    /// <param name="minValue">The inclusive lower bound of the random number. Default is 0</param>
    /// <param name="maxValue">The exclusive upper bound of the random number. Default is int.MaxValue</param>
    /// <returns>A random integer between minValue and maxValue.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if minValue is greater than or equal to maxValue.</exception>
    public static int GetRandomInt(int minValue = 0, int maxValue = int.MaxValue)
    {
        if (minValue >= maxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(minValue), "minValue must be less than maxValue.");
        }

        int range = maxValue - minValue;

        if ((range & (range - 1)) == 0)
        {
            return minValue + (int)(GenerateRandom() & ((ulong)range - 1));
        }

        ulong bits, value;

        do
        {
            bits = GenerateRandom();
            value = bits % (ulong)range;
        }
        while (bits - value + (ulong)(range - 1) < bits);

        return minValue + (int)value;
    }

    /// <summary>
    /// Generates a random double between 0.0 and 1.0.
    /// </summary>
    /// <returns>A random double between 0.0 and 1.0.</returns>
    public static double GetRandomDouble()
    {
        const ulong mask = (1UL << 53) - 1;
        ulong randomValue = GenerateRandom();
        randomValue &= mask;

        return randomValue * (1.0 / (1UL << 53));
    }

    /// <summary>
	/// Generates a random float number in the specified range.
	/// </summary>
	/// <param name="minValue">The inclusive lower bound of the range. Default is 0</param>
	/// <param name="maxValue">The exclusive upper bound of the range. Default is float.MaxValue</param>
	/// <returns>A random float number between minValue and maxValue.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if minValue is greater than or equal to maxValue.</exception>
	public static float GetRandomFloat(float minValue = 0, float maxValue = float.MaxValue)
    {
        if (minValue >= maxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(minValue), "minValue must be less than maxValue.");
        }

        return minValue + (float)(GetRandomDouble() * (maxValue - minValue));
    }

    /// <summary>
    /// Generates a random Gaussian number (normal distribution with mean 0 and standard deviation 1).
    /// </summary>
    /// <returns>A random Gaussian value from a normal distribution with mean 0 and standard deviation 1.</returns>
    public static double GetRandomGaussian()
    {
        (bool HasSpare, double Spare) = _gaussianState.Value;

        if (HasSpare)
        {
            _gaussianState.Value = (false, Spare);
            return Spare;
        }

        double u1, u2, sqrtLog, angle;

        do
        {
            u1 = GetRandomDouble();
            u2 = GetRandomDouble();
        }
        while (u1 <= 0);

        sqrtLog = Math.Sqrt(-2.0 * Math.Log(u1));
        angle = 2.0 * Math.PI * u2;

        double spare = sqrtLog * Math.Sin(angle);

        _gaussianState.Value = (true, spare);

        return sqrtLog * Math.Cos(angle);
    }

    /// <summary>
    /// Generates an array of random bytes of the specified length.
    /// </summary>
    /// <param name="length">The length of the byte array.</param>
    /// <returns>A byte array containing random bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the length is less than or equal to 0.</exception>
    public static byte[] GetRandomBytes(int length)
    {
        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be greater than 0.");
        }

        byte[] bytes = new byte[length];
        Span<byte> randomBytes = stackalloc byte[8];

        int fullChunks = length / 8;
        int remainingBytes = length % 8;

        for (int i = 0; i < fullChunks; i++)
        {
            ulong randomValue = GenerateRandom();
            BitConverter.TryWriteBytes(randomBytes, randomValue);
            randomBytes.CopyTo(bytes.AsSpan(i * 8));
        }

        if (remainingBytes > 0)
        {
            ulong randomValue = GenerateRandom();
            BitConverter.TryWriteBytes(randomBytes, randomValue);
            randomBytes[..remainingBytes].CopyTo(bytes.AsSpan(fullChunks * 8));
        }

        return bytes;
    }

    /// <summary>
    /// Returns a random value from the specified enumeration type.
    /// </summary>
    /// <typeparam name="T">The enumeration type from which to select a random value.</typeparam>
    /// <returns>A randomly selected value of the specified enumeration type.</returns>
    public static T GetRandomEnumValue<T>() where T : struct, Enum
    {
        T[] values = Enum.GetValues<T>();
        return values[GetRandomInt(maxValue: values.Length)];
    }

    /// <summary>
    /// Returns a random element from the given collection.
    /// </summary>
    /// <param name="collection">The collection to select a random element from.</param>
    /// <returns>A random element from the collection.</returns>
    /// <exception cref="ArgumentException">Thrown if the collection is null or empty.</exception>
    public static T GetRandomElement<T>(IList<T> collection)
    {
        if (collection == null)
        {
            throw new ArgumentNullException(nameof(collection), "Collection cannot be null.");
        }

        if (collection.Count == 0)
        {
            throw new ArgumentException("Collection cannot be empty.", nameof(collection));
        }

        return collection[GetRandomInt(0, collection.Count)];
    }

    /// <summary>
    /// Generates a random DateTime between the specified minDate and maxDate.
    /// </summary>
    /// <param name="minDate">The lower bound for the random DateTime. null is DateTime.MinValue.</param>
    /// <param name="maxDate">The upper bound for the random DateTime. null is DateTime.MaxValue.</param>
    /// <returns>A random DateTime value between minDate and maxDate.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if minDate is greater than or equal to maxDate.</exception>    
    public static DateTime GetRandomDateTime(DateTime? minDate = null, DateTime? maxDate = null)
    {
        minDate ??= DateTime.MinValue;
        maxDate ??= DateTime.MaxValue;

        if (minDate >= maxDate)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minDate),
                $"minDate ({minDate:yyyy-MM-ddTHH:mm:ss.fff}) must be earlier than maxDate ({maxDate:yyyy-MM-ddTHH:mm:ss.fff})."
            );
        }

        long range = maxDate.Value.Ticks - minDate.Value.Ticks;
        long randomTicks = minDate.Value.Ticks + (long)(GenerateRandom() % (ulong)range);
        return new DateTime(randomTicks);
    }

    /// <summary>
    /// Generates a random character from a specified inclusive character range, based on Unicode values.
    /// The default range is from space (' ') to tilde ('~'), covering printable ASCII characters.
    /// </summary>
    /// <param name="minChar">The inclusive lower bound of the character range. Default is the space character (' ').</param>
    /// <param name="maxChar">The inclusive upper bound of the character range. Default is the tilde character ('~').</param>
    /// <returns>A random character within the specified range.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="minChar"/> is greater than or equal to <paramref name="maxChar"/>.</exception>

    public static char GetRandomChar(char minChar = ' ', char maxChar = '~')
    {
        if (minChar >= maxChar)
        {
            throw new ArgumentOutOfRangeException(nameof(minChar), "minChar must be less than or equal to maxChar.");
        }

        int min = minChar;
        int max = maxChar + 1;

        return (char)GetRandomInt(min, max);
    }

    /// <summary>
    /// Generates a random string of the specified length using selected character sets.
    /// By default, it includes lowercase letters, uppercase letters, and numbers.
    /// Additional character sets can be included or excluded via the <paramref name="options"/> parameter.
    /// </summary>
    /// <param name="length">The desired length of the generated string. Must be greater than zero.</param>
    /// <param name="options">
    /// Specifies the character sets to include in the string. 
    /// Default is a combination of lowercase letters, uppercase letters, and numbers.
    /// </param>
    /// <returns>A random string of the specified length, generated from the selected character sets.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="length"/> is less than or equal to zero.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when no valid character sets are selected via <paramref name="options"/>.
    /// </exception>
    public static string GetRandomString(int length, RandomStringOptions options = RandomStringOptions.IncludeLowercaseLetters | RandomStringOptions.IncludeUppercaseLetters | RandomStringOptions.IncludeNumbers)
    {
        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be greater than zero.");
        }

        List<char> chars = [];

        foreach (var option in RandomCharacterSets)
        {
            if (options.HasFlag(option.Key))
            {
                chars.AddRange(option.Value);
            }
        }

        if (chars.Count == 0)
        {
            throw new ArgumentException("No valid character sets were selected based on the provided RandomStringOptions.", nameof(options));
        }

        Span<char> result = stackalloc char[length];

        for (int i = 0; i < length; i++)
        {
            result[i] = chars[GetRandomInt(0, chars.Count)];
        }

        return new string(result);
    }

    /// <summary>
	/// Generates a random geographic coordinate (latitude, longitude).
	/// </summary>
	/// <returns>A tuple representing latitude and longitude.</returns>
	public static (double Latitude, double Longitude) GetRandomGeoCoordinate()
    {
        double latitude = GetRandomDouble() * 180.0 - 90.0;
        double longitude = GetRandomDouble() * 360.0 - 180.0;

        return (Math.Round(latitude, 6), Math.Round(longitude, 6));
    }

    /// <summary>
    /// Generates a random hexadecimal color code.
    /// </summary>
    /// <returns>A hex color code (e.g., "#FF5733").</returns>
    public static string GetRandomHexColor()
    {
        return $"#{GetRandomInt(0, 256):X2}{GetRandomInt(0, 256):X2}{GetRandomInt(0, 256):X2}";
    }

    /// <summary>
    /// Shuffles the elements in the provided list.
    /// </summary>
    /// <param name="values">The list to shuffle.</param>
    public static void Shuffle<T>(List<T> values)
    {
        if (values.Count <= 1)
        {
            return;
        }

        int listCount = values.Count;

        for (int i = listCount - 1; i > 0; i--)
        {
            int j = GetRandomInt(0, i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }
}