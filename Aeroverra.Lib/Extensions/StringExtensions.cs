using System.Runtime.Serialization;

namespace Aeroverra.Lib.Extensions
{
    public static class StringExtensions
    {
        extension(string value)
        {
            /// <summary>
            /// Converts the string value to the specified enum type.   
            /// </summary>
            /// <remarks>The method first attempts standard enum parsing (case-insensitive). If that
            /// fails, it checks for EnumMemberAttribute values on enum fields.</remarks>
            /// <typeparam name="TEnum">The enum type to convert to.</typeparam>
            /// <returns>The enum value corresponding to the string value.</returns>
            /// <exception cref="ArgumentNullException">The value is null or whitespace.</exception>
            /// <exception cref="ArgumentException">The value does not match any defined enum value or EnumMemberAttribute value.</exception>
            public TEnum ToEnum<TEnum>() where TEnum : struct, Enum
            {
                // 1. Handle Null/Empty
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentNullException(nameof(value), "Value cannot be null or empty.");
                }

                // 2. FAST PATH: Standard Parse first
                if (Enum.TryParse<TEnum>(value, true, out var result) && Enum.IsDefined(typeof(TEnum), result))
                {
                    return result;
                }

                // 3. SLOW PATH: Check Attributes
                // It only runs this reflection loop if TryParse fails.
                var type = typeof(TEnum);
                foreach (var field in type.GetFields())
                {
                    if (Attribute.GetCustomAttribute(field, typeof(EnumMemberAttribute)) is EnumMemberAttribute attribute)
                    {
                        // Compare the input string to the Value property of the attribute
                        if (string.Equals(attribute.Value, value, StringComparison.OrdinalIgnoreCase))
                        {
                            return (TEnum)field.GetValue(null)!;
                        }
                    }
                }

                // 4. Throw on Failure
                throw new ArgumentException($"Value '{value}' is not valid for Enum '{typeof(TEnum).Name}'.");
            }
        }
    }
}

