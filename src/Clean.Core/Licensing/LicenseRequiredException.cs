namespace Clean.Core.Licensing;

public sealed class LicenseRequiredException() : InvalidOperationException("Une licence est nécessaire pour retirer des fichiers.");
