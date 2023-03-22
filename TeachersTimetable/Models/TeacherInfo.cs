namespace TeachersTimetable.Models;

public class TeacherInfo
{
    public string Name { get; set; }
    public string Date { get; set; }
    public List<Lesson> Lessons = new();
}