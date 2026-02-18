using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace LUSharp.Project.Rojo
{
    /// <summary>
    /// Manager class that helps connect projects to ROJO.
    /// </summary>
    public static class RojoManager
    {
        public static void WriteConfig(string path, RojoConfig config)
        {
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                Converters = { new RojoInstanceConverter() }
            };

            File.WriteAllText(path, JsonConvert.SerializeObject(config, settings));
        }

        public static RojoConfig ReadConfig(string path, RojoConfig config)
        {
            return JsonConvert.DeserializeObject<RojoConfig>(File.ReadAllText(path));
        }
    }
    public class RojoInstanceConverter : JsonConverter<RojoInstance>
    {
        public override void WriteJson(JsonWriter writer, RojoInstance value, JsonSerializer serializer)
        {
            writer.WriteStartObject();

            if (value.ClassName != null)
            {
                writer.WritePropertyName("$className");
                writer.WriteValue(value.ClassName);
            }

            if (value.Path != null)
            {
                writer.WritePropertyName("$path");
                writer.WriteValue(value.Path);
            }

            if (value.Properties != null)
            {
                writer.WritePropertyName("$properties");
                serializer.Serialize(writer, value.Properties);
            }

            foreach (var child in value.Children)
            {
                writer.WritePropertyName(child.Key);
                serializer.Serialize(writer, child.Value);
            }

            writer.WriteEndObject();
        }

        public override RojoInstance ReadJson(
            JsonReader reader,
            Type objectType,
            RojoInstance existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            var obj = JObject.Load(reader);
            var inst = new RojoInstance();

            inst.ClassName = obj["$className"]?.ToString();
            inst.Path = obj["$path"]?.ToString();
            inst.Properties = obj["$properties"]?.ToObject<Dictionary<string, object>>();

            foreach (var prop in obj.Properties())
            {
                if (prop.Name.StartsWith("$")) continue;
                inst.Children[prop.Name] = prop.Value.ToObject<RojoInstance>(serializer);
            }

            return inst;
        }
    }

}
