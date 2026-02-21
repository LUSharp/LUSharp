local CompletionTemplates = {}

local TEMPLATES = {
    {
        label = "for",
        detail = "snippet",
        documentation = "for loop",
        insertText = "for (int ${1:i} = 0; ${1:i} < ${2:length}; ${1:i}++)\n{\n    $0\n}",
    },
    {
        label = "foreach",
        detail = "snippet",
        documentation = "foreach loop",
        insertText = "foreach (var ${1:item} in ${2:collection})\n{\n    $0\n}",
    },
    {
        label = "if",
        detail = "snippet",
        documentation = "if statement",
        insertText = "if (${1:condition})\n{\n    $0\n}",
    },
    {
        label = "try",
        detail = "snippet",
        documentation = "try/catch block",
        insertText = "try\n{\n    $1\n}\ncatch (${2:Exception} ${3:ex})\n{\n    $0\n}",
    },
}

local function startsWithIgnoreCase(value, prefix)
    local normalizedValue = string.lower(value or "")
    local normalizedPrefix = string.lower(prefix or "")

    if normalizedPrefix == "" then
        return true
    end

    return string.sub(normalizedValue, 1, #normalizedPrefix) == normalizedPrefix
end

function CompletionTemplates.getCompletions(prefix)
    local result = {}

    for _, template in ipairs(TEMPLATES) do
        if startsWithIgnoreCase(template.label, prefix) then
            table.insert(result, {
                label = template.label,
                kind = "template",
                detail = template.detail,
                documentation = template.documentation,
                source = "template",
                insertMode = "snippetExpand",
                insertText = template.insertText,
            })
        end
    end

    table.sort(result, function(a, b)
        return a.label < b.label
    end)

    return result
end

return CompletionTemplates
