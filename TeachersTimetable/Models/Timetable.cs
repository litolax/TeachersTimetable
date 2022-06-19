using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TeachersTimetable.Models;

public class Timetable
{
    public ObjectId Id { get; set; }
    public string Date { get; set; } = "";
    [BsonIgnore] public List<Dictionary<string, List<Lesson>>> Table { get; set; } = new(); // Массив (учителя, пары)
}