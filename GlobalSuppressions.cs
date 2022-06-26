// Suppressing specific code analysis messages to be able to see more useful warnings.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Performance",
    "HAA0601:Value type to reference type conversion causing boxing allocation",
    Justification = "Chore.")]

[assembly: SuppressMessage("Performance",
    "HAA0302:Display class allocation to capture closure",
    Justification = "Intentional capture to simplify code.")]

[assembly: SuppressMessage("Performance",
    "HAA0301:Closure Allocation Source",
    Justification = "Intentional capture to simplify code.")]

[assembly: SuppressMessage("Performance",
    "HAA0101:Array allocation for params parameter",
    Justification = "Simpler code with params.")]

[assembly: SuppressMessage("Performance",
    "HAA0603:Delegate allocation from a method group",
    Justification = "I mean, how else...")]

[assembly: SuppressMessage("Performance",
    "HAA0401:Possible allocation of reference type enumerator",
    Justification = "Keen code, cannot be avoided")]
