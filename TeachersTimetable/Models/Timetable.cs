using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TeachersTimetable.Models;

public class Timetable
{
    public string Date { get; set; } = "";
    [BsonIgnore] public List<TeacherInfo> TeacherInfos { get; set; } = new();
}