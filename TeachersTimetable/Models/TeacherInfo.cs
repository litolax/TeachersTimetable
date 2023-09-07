namespace TeachersTimetable.Models;

public class TeacherInfo
{
    public string Name { get; set; }
    public string Date { get; set; }
    public List<Lesson> Lessons = new();
    
    
    public override bool Equals(object? obj)
    {
        if (obj == null || this.GetType() != obj.GetType())
        {
            return false;
        }

        TeacherInfo other = (TeacherInfo)obj;

        return this.Name == other.Name && this.Date == other.Date && this.Lessons.SequenceEqual(other.Lessons);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(this.Name, this.Date, this.Lessons);
    }
}