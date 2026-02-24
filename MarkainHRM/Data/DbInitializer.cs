using OzarkLMS.Models;
using System.Linq;

namespace OzarkLMS.Data
{
    public static class DbInitializer
    {
        public static void Initialize(AppDbContext context)
        {
            // context.Database.EnsureCreated();

            // Look for any users.
            if (context.Users.Any())
            {
                return;   // DB has been seeded
            }

            var users = new User[]
            {
                new User{Username="student", Password="password", Role="student"},
                new User{Username="instructor", Password="password", Role="instructor"}
            };
            context.Users.AddRange(users);
            context.SaveChanges();

            // Ensure Admin exists
            if (!context.Users.Any(u => u.Role == "admin"))
            {
                context.Users.Add(new User { Username = "admin", Password = "password", Role = "admin" });
                context.SaveChanges();
            }

            var courses = new Course[]
            {
                new Course{Name="Intro to Psychology", Code="PSY 101", Term="Fall 2024", Color="bg-blue-500", Icon="🧠"},
                new Course{Name="Advanced Mathematics", Code="MATH 302", Term="Fall 2024", Color="bg-emerald-500", Icon="📐"},
                new Course{Name="Music Theory", Code="bMUS 105", Term="Fall 2024", Color="bg-purple-500", Icon="🎵"}
            };
            context.Courses.AddRange(courses);
            context.SaveChanges();
            
            // Add some assignments
            var psyCourse = context.Courses.First(c => c.Code == "PSY 101");
            var assignments = new Assignment[]
            {
                new Assignment{CourseId=psyCourse.Id, Title="Research Paper", DueDate=DateTime.UtcNow.AddDays(7), Type="assignment"},
                new Assignment{CourseId=psyCourse.Id, Title="Chapter 1 Quiz", DueDate=DateTime.UtcNow.AddDays(2), Type="quiz"}
            };
            context.Assignments.AddRange(assignments);
            context.SaveChanges();
            // Ensure Main Organization Chat exists
            if (!context.ChatGroups.Any(g => g.IsDefault))
            {
                var admin = context.Users.FirstOrDefault(u => u.Role == "admin");
                var adminId = admin?.Id ?? 1; // Fallback if no admin (shouldn't happen due to check above)

                var defaultChat = new ChatGroup
                {
                    Name = "Main Organization Chat",
                    Description = "Official organization-wide channel",
                    CreatedById = adminId,
                    OwnerId = adminId,
                    IsDefault = true,
                    CreatedDate = DateTime.UtcNow
                };
                context.ChatGroups.Add(defaultChat);
                context.SaveChanges();
            }
        }
    }
}
