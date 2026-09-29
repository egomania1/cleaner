using Clean.Core.Models;

namespace Clean.Core.Interfaces;

public interface IPathValidator
{
    PathValidationResult Validate(string path);
}
