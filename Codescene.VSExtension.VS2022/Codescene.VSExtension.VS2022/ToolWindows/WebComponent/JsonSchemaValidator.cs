using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent;
internal class JsonSchemaValidator
{
    private readonly JSchema _schema;

    public JsonSchemaValidator()
    {
        var exePath = Assembly.GetExecutingAssembly().Location;
        var exeFolder = Path.GetDirectoryName(exePath);
        var localFolder = Path.Combine(exeFolder, "ToolWindows\\WebComponent");
        var schemaString = File.ReadAllText(Path.Combine(localFolder, "webview-schema.json"));
        _schema = JSchema.Parse(schemaString);
    }
    public bool Validate(string jsonText)
    {
        var data = JObject.Parse(jsonText);
        IList<string> errors;
        var result = data.IsValid(schema: _schema, errorMessages: out errors);
        return result;
    }
}
