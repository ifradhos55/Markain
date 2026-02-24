using System.ComponentModel.DataAnnotations;

namespace OzarkLMS.Models
{
    public class Question
    {
        public int Id { get; set; }
        public int AssignmentId { get; set; }
        public Assignment? Assignment { get; set; }
        
        [Required]
        public string Text { get; set; }
        public int Points { get; set; } = 1;
        
        public List<QuestionOption> Options { get; set; } = new List<QuestionOption>();
    }

    public class QuestionOption
    {
        public int Id { get; set; }
        public int QuestionId { get; set; }
        public Question? Question { get; set; }
        
        [Required]
        public string Text { get; set; }
        public bool IsCorrect { get; set; }
    }
}
