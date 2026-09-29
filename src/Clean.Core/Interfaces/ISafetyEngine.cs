using Clean.Core.Models;

namespace Clean.Core.Interfaces;

public interface ISafetyEngine
{
    CleaningDecision Evaluate(ScanItem item);
}
