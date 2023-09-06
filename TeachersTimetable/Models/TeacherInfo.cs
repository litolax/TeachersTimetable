namespace TeachersTimetable.Models;

public class TeacherInfo
{
    public string Name { get; set; }
    public string Date { get; set; }
    public List<Lesson> Lessons = new();
    
    
    public override bool Equals(object? obj)
    {
        if (obj == null || GetType() != obj.GetType())
        {
            return false;
        }

        TeacherInfo other = (TeacherInfo)obj;

        return Name == other.Name &&
               Date == other.Date &&
               Lessons.SequenceEqual(other.Lessons);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Date, Lessons);
    }
}