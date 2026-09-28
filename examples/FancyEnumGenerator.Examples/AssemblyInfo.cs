using FancyEnumGenerator.Attributes;

// Applies to every enum in this project. Kept to settings that are harmless for the other examples
// too (they all already have an Unknown member and don't rely on case-sensitive parsing).
[assembly: FancyEnumDefaults(AllowNoUnknown = true, ParseCaseSensitive = false)]
