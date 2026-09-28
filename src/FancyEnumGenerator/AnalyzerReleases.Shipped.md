; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 0.1.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
HENUM001 | FancyEnum | Error | Missing required unknown member
HENUM002 | FancyEnum | Error | Non-contiguous enum values
HENUM003 | FancyEnum | Error | Invalid mapping configuration
HENUM004 | FancyEnum | Error | Duplicate enum numeric value
HENUM005 | FancyEnum | Error | Missing required custom string mapping
HENUM006 | FancyEnum | Warning | Ambiguous parser token
HENUM007 | FancyEnum | Error | Enum container cannot expose generated extensions
HENUM008 | FancyEnum | Error | Requested TryFormat cannot be generated
HENUM009 | FancyEnum | Error | Requested mapping parser cannot be generated
HENUM010 | FancyEnum | Warning | Flags member value is not zero or a single-bit power of two
HENUM011 | FancyEnum | Error | Requested UTF-8 mapping value cannot be generated
HENUM012 | FancyEnum | Error | Member-set DefaultValue or NotMatched type does not match its property's type
HENUM013 | FancyEnum | Error | Member-set constructor parameter does not resolve to a property
HENUM014 | FancyEnum | Error | Mapped field name collides with a generated member
HENUM015 | FancyEnum | Error | Member-set property can never be set (unsupported type, or read-only with no constructor parameter)
HENUM016 | FancyEnum | Warning | Member-set attribute used on an enum without [FancyEnum]
