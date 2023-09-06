namespace TeachersTimetable.Models;

public class Lesson
{
    public int Index { get; set; }
    public string Cabinet { get; set; }
    public string Group { get; set; }
    
    public override bool Equals(object? obj)
    {
        if (obj == null || GetType() != obj.GetType() || obj is not Lesson other)
        {
            return false;
        }

        return Index == other.Index &&
               Cabinet == other.Cabinet &&
               Group == other.Group;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Index, Cabinet, Group);
    }
}