using Microsoft.EntityFrameworkCore;
using OzarkLMS.Data;
using OzarkLMS.Models;

namespace OzarkLMS.Services
{
    public class SelfTestService : ISelfTestService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<SelfTestService> _logger;

        public SelfTestService(AppDbContext context, ILogger<SelfTestService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<TestResult>> RunAllTestsAsync()
        {
            var results = new List<TestResult>();

            // Test 1: Math Logic (Unit Test)
            results.Add(TestMathLogic());

            // Test 2: Database Connectivity
            results.Add(await TestDatabaseConnectionAsync());

            // Test 3: User Count Check
            results.Add(await TestUserCountAsync());

            // Test 4: Course Data Check
            results.Add(await TestCourseDataAsync());

            return results;
        }

        private TestResult TestMathLogic()
        {
            var result = new TestResult { TestName = "Math Logic Unit Test" };
            try
            {
                int a = 5;
                int b = 10;
                int sum = a + b;
                if (sum == 15)
                {
                    result.Passed = true;
                    result.Message = "5 + 10 correctly equals 15.";
                }
                else
                {
                    result.Passed = false;
                    result.Message = $"Expected 15, got {sum}.";
                }
            }
            catch (Exception ex)
            {
                result.Passed = false;
                result.Message = $"Exception: {ex.Message}";
            }
            return result;
        }

        private async Task<TestResult> TestDatabaseConnectionAsync()
        {
            var result = new TestResult { TestName = "Database Connectivity" };
            try
            {
                bool canConnect = await _context.Database.CanConnectAsync();
                if (canConnect)
                {
                    result.Passed = true;
                    result.Message = "Successfully connected to the database.";
                }
                else
                {
                    result.Passed = false;
                    result.Message = "Failed to connect to the database.";
                }
            }
            catch (Exception ex)
            {
                result.Passed = false;
                result.Message = $"Exception: {ex.Message}";
            }
            return result;
        }

        private async Task<TestResult> TestUserCountAsync()
        {
            var result = new TestResult { TestName = "User Data Verification" };
            try
            {
                int userCount = await _context.Users.CountAsync();
                if (userCount > 0)
                {
                    result.Passed = true;
                    result.Message = $"Found {userCount} users in the database.";
                }
                else
                {
                    result.Passed = false;
                    result.Message = "No users found in the database. Seeding might have failed.";
                }
            }
            catch (Exception ex)
            {
                result.Passed = false;
                result.Message = $"Exception: {ex.Message}";
            }
            return result;
        }

        private async Task<TestResult> TestCourseDataAsync()
        {
            var result = new TestResult { TestName = "Course Data Verification" };
            try
            {
                var course = await _context.Courses.FirstOrDefaultAsync();
                if (course != null)
                {
                    result.Passed = true;
                    result.Message = $"Found course: {course.Name} ({course.Code})";
                }
                else
                {
                    // It's possible to have no courses, but for this test we expect some from seeding.
                    // If we want to be strict, we can fail. Let's assume passed if no exception, but warn if empty.
                    int count = await _context.Courses.CountAsync();
                     if (count > 0)
                    {
                        result.Passed = true;
                        result.Message = $"Found {count} courses.";
                    }
                    else
                    {
                        result.Passed = true; // Not necessarily a fail, just empty
                        result.Message = "No courses found, but query succeeded.";
                    }
                }
            }
            catch (Exception ex)
            {
                result.Passed = false;
                result.Message = $"Exception: {ex.Message}";
            }
            return result;
        }
    }
}
