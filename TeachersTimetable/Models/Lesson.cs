namespace TeachersTimetable.Models;

public class Lesson
{
    public int Index { get; set; }
    public string Cabinet { get; set; }
    public string Group { get; set; }
    
    public override bool Equals(object? obj)
    {
        if (obj == null || this.GetType() != obj.GetType() || obj is not Lesson other)
        {
            return false;
        }

        return this.Index == other.Index && this.Cabinet == other.Cabinet && this.Group == other.Group;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(this.Index, this.Cabinet, this.Group);
    }
}