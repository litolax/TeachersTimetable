namespace TeachersTimetable.Models;

public class Lesson
{
    public int Index { get; set; }
    public string Cabinet { get; set; }
    public string Group { get; set; }
    
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            
            hash = hash * 31 + this.Index.GetHashCode();
            hash = hash * 31 + this.Cabinet.GetHashCode();
            hash = hash * 31 + this.Group.GetHashCode();

            return hash;
        }
    }
}