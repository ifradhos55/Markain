using OzarkLMS.Models;

namespace OzarkLMS.Services
{
    public interface ISelfTestService
    {
        Task<List<TestResult>> RunAllTestsAsync();
    }
}
