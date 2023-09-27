[![Issues][issues-shield]][issues-url]

<h3 align="center">Teachers Timetable Bot</h3>

<p align="center">
  Telegram bot for sending actual info about timetable for teachers.
  <br>
  <a href="https://github.com/litolax/TeachersTimetable/issues">Report Bug</a>
  ·
  <a href="https://github.com/litolax/TeachersTimetable/issues">Request Feature</a>
</p>
</div>

<!-- About the project -->
## About the project

This telegram bot can notify teachers about their updated timetables.

### Built With

* [.NET 6.0](https://dotnet.microsoft.com/en-us/download)

### Database
* [mongoDB](https://www.mongodb.com/try/download/community)

<!-- GETTING STARTED -->
## Getting Started
### Installation

1. Fork the git repository.
2. Clone the repository.
3. Install .NET 6.0.
   * [.NET 6.0](https://dotnet.microsoft.com/en-us/download)
4. Create a `config.json` file in the root program directory. Example:
   ```json
   {
    "DbName": "",
    "Host": "",
    "Port": 27017,
    "AuthorizationName": "",
    "AuthorizationPassword": "",
    "Token": "",
    "Administrators": [
     ...
    ],
    "Teachers": [
     ...
    ]
    }
   ```
    - "Administrators: an array of Telegram IDs."
    - "Teachers: an array of teacher records."
    - "To retrieve this array, you can use the following C# code:
```c#
using System.Text.Json;

const string inputString = "";/teachers list via string with separator ,
var teacherArray = inputString.Split(new[] { "," }, StringSplitOptions.None);
var jsonData = JsonSerializer.Serialize(new { Teachers = teacherArray });
File.WriteAllText("Teachers.json", jsonData);
Console.WriteLine("Data has been written to Teachers.json");
```

6. Install mongoDB.
   * [mongoDB](https://www.mongodb.com/try/download/community)
7. Install geckodriver (for week screenshots).
   * [geckodriver](https://github.com/mozilla/geckodriver/releases)

### Build
#### Linux
```markdown
dotnet publish TeachersTimetable -c Release -r ubuntu.21.04-x64 -p:PublishSingleFile=true --self-contained true
```

<!-- CONTACT -->
## Contact
Feel free to create issues, features, or pull requests.
<br>
Discord: config.json#8501
<br>
Project Link: [https://github.com/litolax/TeachersTimetable](https://github.com/litolax/TeachersTimetable)

<!-- MARKDOWN LINKS & IMAGES -->
<!-- https://www.markdownguide.org/basic-syntax/#reference-style-links -->
[issues-shield]: https://img.shields.io/github/issues/litolax/TeachersTimetable.svg?style=for-the-badge
[issues-url]: https://github.com/litolax/TeachersTimetable/issues
