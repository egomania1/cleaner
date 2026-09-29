using Clean.Core.Models;

namespace Clean.App.ViewModels;

public sealed record StorageBucketRow(StorageUsage Usage, string Label, string SizeText);
