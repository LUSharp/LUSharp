using LUSharpApiGenerator.Models;

namespace LUSharpApiGenerator.Filtering;

public class ApiFilter
{
    public (List<ApiClass> Classes, List<ApiEnum> Enums) Filter(ApiDump dump)
    {
        var classes = new List<ApiClass>();

        foreach (var cls in dump.Classes)
        {
            // Skip root placeholder
            if (cls.Name == "<<<ROOT>>>")
                continue;

            // Skip NotScriptable classes
            if (cls.Tags?.Contains("NotScriptable") == true)
                continue;

            // Filter members
            cls.Members = FilterMembers(cls.Members);
            classes.Add(cls);
        }

        // Enums: keep all (filter deprecated items)
        var enums = new List<ApiEnum>();
        foreach (var e in dump.Enums)
        {
            var filtered = new ApiEnum
            {
                Name = e.Name,
                Tags = e.Tags,
                Items = e.Items
                    .Where(item => item.Tags?.Contains("Deprecated") != true)
                    .ToList()
            };
            if (filtered.Items.Count > 0)
                enums.Add(filtered);
        }

        return (classes, enums);
    }

    private List<ApiMember> FilterMembers(List<ApiMember> members)
    {
        var result = new List<ApiMember>();

        foreach (var member in members)
        {
            // Skip NotScriptable
            if (member.HasTag("NotScriptable"))
                continue;

            // Skip Deprecated
            if (member.HasTag("Deprecated"))
                continue;

            // Skip Hidden
            if (member.HasTag("Hidden"))
                continue;

            switch (member)
            {
                case ApiProperty prop:
                    // Skip if both read and write are secured
                    if (prop.Security.Read != "None" && prop.Security.Write != "None")
                        continue;
                    // Keep if at least read is accessible
                    result.Add(prop);
                    break;

                case ApiFunction func:
                    if (func.Security != "None")
                        continue;
                    result.Add(func);
                    break;

                case ApiEvent evt:
                    if (evt.Security != "None")
                        continue;
                    result.Add(evt);
                    break;

                case ApiCallback cb:
                    if (cb.Security != "None")
                        continue;
                    result.Add(cb);
                    break;
            }
        }

        return result;
    }
}
