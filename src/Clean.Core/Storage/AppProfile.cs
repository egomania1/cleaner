namespace Clean.Core.Storage;

public sealed record AppProfile(
    string Name,
    string Category,
    string WhatItIs,
    string WhereSpaceGoes,
    IReadOnlyList<string> Keys);
