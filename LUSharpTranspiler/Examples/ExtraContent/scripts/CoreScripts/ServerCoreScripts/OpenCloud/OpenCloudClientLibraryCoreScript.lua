local TABLE_TYPE = "table"
local NUMBER_TYPE = "number"
local STRING_TYPE = "string"
local BOOLEAN_TYPE = "boolean"
local BUFFER_TYPE = "buffer"
local INVALID_ARGUMENT = "INVALID_ARGUMENT"
local URL = "Url"
local BODY = "Body"
local REQUEST_TYPE = "Request_Type"
local NIL_REQUEST_ERROR_MESSAGE = "Request provided was nil."
local HEADERS = "Headers"
local API_KEY_HEADER = "x-api-key"

local ContentProvider = game:GetService("ContentProvider")
local HttpService = game:GetService("HttpService")
local OpenCloudService = game:GetService("OpenCloudService")
local RunService = game:GetService("RunService")

function InvalidArgumentError(message: string) : any 
    return {
        ["StatusCode"] = 400,
        ["Body"] = HttpService:JSONEncode({
            ["code"] = INVALID_ARGUMENT,
            ["message"] = message
        })
    }
end

--[[
    All of the verify functions (verifyString, verifyGetUserRequest, etc.) either return:
    
        1. nil if the argument follows the correct types
        
        2. an InvalidArgumentError if there is any part that is an incorrect type
           (still a valid response but indicates that an invalid argument was provided)
]]
function verifyTable(tableArgument, argumentName) : any
    if tableArgument == nil then
        return nil
    end
    
    if typeof(tableArgument) ~= TABLE_TYPE then
        return InvalidArgumentError(`Argument not a table: {argumentName}.`)
    end
    
    return nil
end

function verifyNumber(numberArgument, argumentName) : any
    if numberArgument == nil then
        return nil
    end
    
    if typeof(numberArgument) ~= NUMBER_TYPE then
        return InvalidArgumentError(`Argument not a number: {argumentName}.`)
    end
    
    return nil
end

function verifyString(stringArgument, argumentName) : any
    if stringArgument == nil then
        return nil
    end
    
    if typeof(stringArgument) ~= STRING_TYPE then
        return InvalidArgumentError(`Argument not a string: {argumentName}.`)
    end
    
    return nil
end

function verifyEnum(enumArgument, argumentName) : any
    if enumArgument == nil then
        return nil
    end
    
    if typeof(enumArgument) ~= NUMBER_TYPE and typeof(enumArgument) ~= STRING_TYPE then
        return InvalidArgumentError(`Argument not an enum: {argumentName}.`)
    end
    
    return nil
end

function getApisUrl()
    local baseUrl = ContentProvider.BaseUrl:lower()
    baseUrl = string.gsub(baseUrl, "http:", "https:")
    return string.gsub(baseUrl, "www", "apis")
end

function getUrlPrefix(headers : any)
    if headers ~= nil and headers[API_KEY_HEADER] ~= nil then
        return ""
    end
    if RunService:IsStudio() then
        return "user"
    end
    return "rcc"
end

function verifyGetUniverseRequest(getUniverseRequest, argumentName) : any
    if getUniverseRequest == nil then
        return nil
    end
    
    local res = verifyTable(getUniverseRequest, argumentName)
    if res ~= nil then
        return res
    end
    
    if getUniverseRequest.path == nil then
        return InvalidArgumentError(`Required argument was not provided: {argumentName}.path.`)
    end
    res = verifyString(getUniverseRequest.path, `{argumentName}.path`)
    if res ~= nil then
        return res
    end
    
    return nil
end

function getUniverseUrl(getUniverseRequest : any, headers : any) : string
    if getUniverseRequest.path == nil then
        return InvalidArgumentError(`URL parameter provided was nil: getUniverseRequest.path.`)
    end
    if string.match(getUniverseRequest.path, "^universes/([^/]+)$") == nil then
        return InvalidArgumentError(`URL parameter was not formatted correctly: getUniverseRequest.path.`)
    end
    
    local url = string.format("%s%s/cloud/v2/%s", getApisUrl(), getUrlPrefix(headers), tostring(getUniverseRequest.path))
    
    return url
end

function getUniverse(getUniverseRequest : any, headers : any)
    if getUniverseRequest == nil then
        return InvalidArgumentError(NIL_REQUEST_ERROR_MESSAGE)
    end
    
    local res = verifyGetUniverseRequest(getUniverseRequest, `getUniverseRequest`)
    if res ~= nil then
        return res
    end

    local url = getUniverseUrl(getUniverseRequest, headers)
    if typeof(url) ~= STRING_TYPE then
        return url
    end

    return OpenCloudService:HttpRequestAsync({[URL] = url, [REQUEST_TYPE] = "GET", [HEADERS] = headers})
end

OpenCloudService:RegisterOpenCloud("v2", "GetUniverse", getUniverse)

function verifyTranslateTextRequest(translateTextRequest, argumentName) : any
    if translateTextRequest == nil then
        return nil
    end
    
    local res = verifyTable(translateTextRequest, argumentName)
    if res ~= nil then
        return res
    end
    
    if translateTextRequest.path == nil then
        return InvalidArgumentError(`Required argument was not provided: {argumentName}.path.`)
    end
    res = verifyString(translateTextRequest.path, `{argumentName}.path`)
    if res ~= nil then
        return res
    end
    
    if translateTextRequest.text == nil then
        return InvalidArgumentError(`Required argument was not provided: {argumentName}.text.`)
    end
    res = verifyString(translateTextRequest.text, `{argumentName}.text`)
    if res ~= nil then
        return res
    end
    
    res = verifyString(translateTextRequest.sourceLanguageCode, `{argumentName}.sourceLanguageCode`)
    if res ~= nil then
        return res
    end
    
    for index, value in translateTextRequest.targetLanguageCodes do
        res = verifyString(value, `{argumentName}.targetLanguageCodes[{index}]`)
        if res ~= nil then
            return res
        end
    end
    
    return nil
end

function translateTextUrl(translateTextRequest : any) : string
    if translateTextRequest.path == nil then
        return InvalidArgumentError(`URL parameter provided was nil: translateTextRequest.path.`)
    end
    if string.match(translateTextRequest.path, "^universes/([^/]+)$") == nil then
        return InvalidArgumentError(`URL parameter was not formatted correctly: translateTextRequest.path.`)
    end
    
    local url = string.format("%s%s/cloud/v2/%s:translateText", getApisUrl(), getUrlPrefix(), tostring(translateTextRequest.path))
    
    return url
end

function translateText(translateTextRequest : any)
    if translateTextRequest == nil then
        return InvalidArgumentError(NIL_REQUEST_ERROR_MESSAGE)
    end
    
    local res = verifyTranslateTextRequest(translateTextRequest, `translateTextRequest`)
    if res ~= nil then
        return res
    end
    
    local url = translateTextUrl(translateTextRequest)
    if typeof(url) ~= STRING_TYPE then
        return url
    end
    
    local bodyString = HttpService:JSONEncode(translateTextRequest)
    
    return OpenCloudService:HttpRequestAsync({[URL] = url, [REQUEST_TYPE] = "POST", [BODY] = bodyString})
end

OpenCloudService:RegisterOpenCloud("v2", "TranslateText", translateText)

function verifyGenerateSpeechRequest(generateSpeechRequest, argumentName) : any
    if generateSpeechRequest == nil then
        return nil
    end
    
    local res = verifyTable(generateSpeechRequest, argumentName)
    if res ~= nil then
        return res
    end
    
    if generateSpeechRequest.path == nil then
        return InvalidArgumentError(`Required argument was not provided: {argumentName}.path.`)
    end
    res = verifyString(generateSpeechRequest.path, `{argumentName}.path`)
    if res ~= nil then
        return res
    end
    
    if generateSpeechRequest.text == nil then
        return InvalidArgumentError(`Required argument was not provided: {argumentName}.text.`)
    end
    res = verifyString(generateSpeechRequest.text, `{argumentName}.text`)
    if res ~= nil then
        return res
    end
    
    res = verifyGeneratedSpeechStyle(generateSpeechRequest.speechStyle, `{argumentName}.speechStyle`)
    if res ~= nil then
        return res
    end
    
    return nil
end
    
function verifyGeneratedSpeechStyle(generatedSpeechStyle, argumentName) : any
    if generatedSpeechStyle == nil then
        return nil
    end
    
    local res = verifyTable(generatedSpeechStyle, argumentName)
    if res ~= nil then
        return res
    end
    
    res = verifyString(generatedSpeechStyle.voiceId, `{argumentName}.voiceId`)
    if res ~= nil then
        return res
    end
    
    res = verifyNumber(generatedSpeechStyle.pitch, `{argumentName}.pitch`)
    if res ~= nil then
        return res
    end
    
    res = verifyNumber(generatedSpeechStyle.speed, `{argumentName}.speed`)
    if res ~= nil then
        return res
    end
    
    return nil
end

function generateSpeechUrl(generateSpeechRequest : any) : string
    if generateSpeechRequest.path == nil then
        return InvalidArgumentError(`URL parameter provided was nil: generateSpeechRequest.path.`)
    end
    if string.match(generateSpeechRequest.path, "^universes/([^/]+)$") == nil then
        return InvalidArgumentError(`URL parameter was not formatted correctly: generateSpeechRequest.path.`)
    end
    
    local url = string.format("%s%s/cloud/v2/%s:generateSpeech", getApisUrl(), getUrlPrefix(), tostring(generateSpeechRequest.path))
    
    return url
end

function generateSpeech(generateSpeechRequest : any)
    if generateSpeechRequest == nil then
        return InvalidArgumentError(NIL_REQUEST_ERROR_MESSAGE)
    end
    
    local res = verifyGenerateSpeechRequest(generateSpeechRequest, `generateSpeechRequest`)
    if res ~= nil then
        return res
    end
    
    local url = generateSpeechUrl(generateSpeechRequest)
    if typeof(url) ~= STRING_TYPE then
        return url
    end
    
    local bodyString = HttpService:JSONEncode(generateSpeechRequest)
    
    return OpenCloudService:HttpRequestAsync({[URL] = url, [REQUEST_TYPE] = "POST", [BODY] = bodyString})
end

OpenCloudService:RegisterOpenCloud("v2", "GenerateSpeech", generateSpeech)

function verifyGenerateTextRequest(generateTextRequest, argumentName) : any
    if generateTextRequest == nil then
        return nil
    end
    
    local res = verifyTable(generateTextRequest, argumentName)
    if res ~= nil then
        return res
    end
    
    if generateTextRequest.path == nil then
        return InvalidArgumentError(`Required argument was not provided: {argumentName}.path.`)
    end
    res = verifyString(generateTextRequest.path, `{argumentName}.path`)
    if res ~= nil then
        return res
    end
    
    res = verifyString(generateTextRequest.userPrompt, `{argumentName}.userPrompt`)
    if res ~= nil then
        return res
    end
    
    res = verifyString(generateTextRequest.systemPrompt, `{argumentName}.systemPrompt`)
    if res ~= nil then
        return res
    end
    
    res = verifyNumber(generateTextRequest.temperature, `{argumentName}.temperature`)
    if res ~= nil then
        return res
    end
    
    res = verifyNumber(generateTextRequest.topP, `{argumentName}.topP`)
    if res ~= nil then
        return res
    end
    
    res = verifyNumber(generateTextRequest.maxTokens, `{argumentName}.maxTokens`)
    if res ~= nil then
        return res
    end
    
    res = verifyNumber(generateTextRequest.seed, `{argumentName}.seed`)
    if res ~= nil then
        return res
    end
    
    res = verifyString(generateTextRequest.contextToken, `{argumentName}.contextToken`)
    if res ~= nil then
        return res
    end
    
    res = verifyString(generateTextRequest.model, `{argumentName}.model`)
    if res ~= nil then
        return res
    end
    
    return nil
end

function generateTextUrl(generateTextRequest : any) : string
    if generateTextRequest.path == nil then
        return InvalidArgumentError(`URL parameter provided was nil: generateTextRequest.path.`)
    end
    if string.match(generateTextRequest.path, "^universes/([^/]+)$") == nil then
        return InvalidArgumentError(`URL parameter was not formatted correctly: generateTextRequest.path.`)
    end
    
    local url = string.format("%s%s/cloud/v2/%s:generateText", getApisUrl(), getUrlPrefix(), tostring(generateTextRequest.path))
    
    return url
end

function generateText(generateTextRequest : any)
    if generateTextRequest == nil then
        return InvalidArgumentError(NIL_REQUEST_ERROR_MESSAGE)
    end
    
    local res = verifyGenerateTextRequest(generateTextRequest, `generateTextRequest`)
    if res ~= nil then
        return res
    end
    
    local url = generateTextUrl(generateTextRequest)
    if typeof(url) ~= STRING_TYPE then
        return url
    end
    
    local bodyString = HttpService:JSONEncode(generateTextRequest)
    
    return OpenCloudService:HttpRequestAsync({[URL] = url, [REQUEST_TYPE] = "POST", [BODY] = bodyString})
end

OpenCloudService:RegisterOpenCloud("v2", "GenerateText", generateText)

function verifyGetPlaceRequest(getPlaceRequest, argumentName) : any
    if getPlaceRequest == nil then
        return nil
    end
    
    local res = verifyTable(getPlaceRequest, argumentName)
    if res ~= nil then
        return res
    end
    
    if getPlaceRequest.path == nil then
        return InvalidArgumentError(`Required argument was not provided: {argumentName}.path.`)
    end
    res = verifyString(getPlaceRequest.path, `{argumentName}.path`)
    if res ~= nil then
        return res
    end
    
    return nil
end

function getPlaceUrl(getPlaceRequest : any, headers : any) : string
    if getPlaceRequest.path == nil then
        return InvalidArgumentError(`URL parameter provided was nil: getPlaceRequest.path.`)
    end
    if string.match(getPlaceRequest.path, "^universes/([^/]+)/places/([^/]+)$") == nil then
        return InvalidArgumentError(`URL parameter was not formatted correctly: getPlaceRequest.path.`)
    end
    
    local url = string.format("%s%s/cloud/v2/%s", getApisUrl(), getUrlPrefix(headers), tostring(getPlaceRequest.path))
    
    return url
end

function getPlace(getPlaceRequest : any, headers : any)
    if getPlaceRequest == nil then
        return InvalidArgumentError(NIL_REQUEST_ERROR_MESSAGE)
    end
    
    local res = verifyGetPlaceRequest(getPlaceRequest, `getPlaceRequest`)
    if res ~= nil then
        return res
    end

    local url = getPlaceUrl(getPlaceRequest, headers)
    if typeof(url) ~= STRING_TYPE then
        return url
    end

    return OpenCloudService:HttpRequestAsync({[URL] = url, [REQUEST_TYPE] = "GET", [HEADERS] = headers})
end

OpenCloudService:RegisterOpenCloud("v2", "GetPlace", getPlace)

function verifyGetGroupRequest(getGroupRequest, argumentName) : any
    if getGroupRequest == nil then
        return nil
    end
    
    local res = verifyTable(getGroupRequest, argumentName)
    if res ~= nil then
        return res
    end
    
    if getGroupRequest.path == nil then
        return InvalidArgumentError(`Required argument was not provided: {argumentName}.path.`)
    end
    res = verifyString(getGroupRequest.path, `{argumentName}.path`)
    if res ~= nil then
        return res
    end
    
    return nil
end

function getGroupUrl(getGroupRequest : any, headers : any) : string
    if getGroupRequest.path == nil then
        return InvalidArgumentError(`URL parameter provided was nil: getGroupRequest.path.`)
    end
    if string.match(getGroupRequest.path, "^groups/([^/]+)$") == nil then
        return InvalidArgumentError(`URL parameter was not formatted correctly: getGroupRequest.path.`)
    end
    
    local url = string.format("%s%s/cloud/v2/%s", getApisUrl(), getUrlPrefix(headers), tostring(getGroupRequest.path))
    
    return url
end

function getGroup(getGroupRequest : any, headers : any)
    if getGroupRequest == nil then
        return InvalidArgumentError(NIL_REQUEST_ERROR_MESSAGE)
    end
    
    local res = verifyGetGroupRequest(getGroupRequest, `getGroupRequest`)
    if res ~= nil then
        return res
    end

    local url = getGroupUrl(getGroupRequest, headers)
    if typeof(url) ~= STRING_TYPE then
        return url
    end

    return OpenCloudService:HttpRequestAsync({[URL] = url, [REQUEST_TYPE] = "GET", [HEADERS] = headers})
end

OpenCloudService:RegisterOpenCloud("v2", "GetGroup", getGroup)

function verifyListInventoryItemsRequest(listInventoryItemsRequest, argumentName) : any
    if listInventoryItemsRequest == nil then
        return nil
    end
    
    local res = verifyTable(listInventoryItemsRequest, argumentName)
    if res ~= nil then
        return res
    end
    
    if listInventoryItemsRequest.parent == nil then
        return InvalidArgumentError(`Required argument was not provided: {argumentName}.parent.`)
    end
    res = verifyString(listInventoryItemsRequest.parent, `{argumentName}.parent`)
    if res ~= nil then
        return res
    end
    
    res = verifyNumber(listInventoryItemsRequest.maxPageSize, `{argumentName}.maxPageSize`)
    if res ~= nil then
        return res
    end
    
    res = verifyString(listInventoryItemsRequest.pageToken, `{argumentName}.pageToken`)
    if res ~= nil then
        return res
    end
    
    res = verifyString(listInventoryItemsRequest.filter, `{argumentName}.filter`)
    if res ~= nil then
        return res
    end
    
    return nil
end

function listInventoryItemsUrl(listInventoryItemsRequest : any, headers : any) : string
    if listInventoryItemsRequest.parent == nil then
        return InvalidArgumentError(`URL parameter provided was nil: listInventoryItemsRequest.parent.`)
    end
    if string.match(listInventoryItemsRequest.parent, "^users/([^/]+)$") == nil then
        return InvalidArgumentError(`URL parameter was not formatted correctly: listInventoryItemsRequest.parent.`)
    end
    
    local url = string.format("%s%s/cloud/v2/%s/inventory-items", getApisUrl(), getUrlPrefix(headers), tostring(listInventoryItemsRequest.parent))
    
    if listInventoryItemsRequest.maxPageSize == nil and listInventoryItemsRequest.pageToken == nil and listInventoryItemsRequest.filter == nil then
        return url
    end
    
    local queryParams = {}
    if listInventoryItemsRequest.maxPageSize ~= nil then
        table.insert(queryParams, string.format("maxPageSize=%s", tostring(listInventoryItemsRequest.maxPageSize)))
    end
    if listInventoryItemsRequest.pageToken ~= nil then
        table.insert(queryParams, string.format("pageToken=%s", tostring(listInventoryItemsRequest.pageToken)))
    end
    if listInventoryItemsRequest.filter ~= nil then
        table.insert(queryParams, string.format("filter=%s", tostring(listInventoryItemsRequest.filter)))
    end
    
    url = string.format("%s?%s", url, table.concat(queryParams, "&"))

    return url
end

function listInventoryItems(listInventoryItemsRequest : any, headers : any)
    if listInventoryItemsRequest == nil then
        return InvalidArgumentError(NIL_REQUEST_ERROR_MESSAGE)
    end
    
    local res = verifyListInventoryItemsRequest(listInventoryItemsRequest, `listInventoryItemsRequest`)
    if res ~= nil then
        return res
    end

    local url = listInventoryItemsUrl(listInventoryItemsRequest, headers)
    if typeof(url) ~= STRING_TYPE then
        return url
    end

    return OpenCloudService:HttpRequestAsync({[URL] = url, [REQUEST_TYPE] = "GET", [HEADERS] = headers})
end

OpenCloudService:RegisterOpenCloud("v2", "ListInventoryItems", listInventoryItems)

function verifyListGroupMembershipsRequest(listGroupMembershipsRequest, argumentName) : any
    if listGroupMembershipsRequest == nil then
        return nil
    end
    
    local res = verifyTable(listGroupMembershipsRequest, argumentName)
    if res ~= nil then
        return res
    end
    
    if listGroupMembershipsRequest.parent == nil then
        return InvalidArgumentError(`Required argument was not provided: {argumentName}.parent.`)
    end
    res = verifyString(listGroupMembershipsRequest.parent, `{argumentName}.parent`)
    if res ~= nil then
        return res
    end
    
    res = verifyNumber(listGroupMembershipsRequest.maxPageSize, `{argumentName}.maxPageSize`)
    if res ~= nil then
        return res
    end
    
    res = verifyString(listGroupMembershipsRequest.pageToken, `{argumentName}.pageToken`)
    if res ~= nil then
        return res
    end
    
    res = verifyString(listGroupMembershipsRequest.filter, `{argumentName}.filter`)
    if res ~= nil then
        return res
    end
    
    return nil
end

function listGroupMembershipsUrl(listGroupMembershipsRequest : any, headers : any) : string
    if listGroupMembershipsRequest.parent == nil then
        return InvalidArgumentError(`URL parameter provided was nil: listGroupMembershipsRequest.parent.`)
    end
    if string.match(listGroupMembershipsRequest.parent, "^groups/([^/]+)$") == nil then
        return InvalidArgumentError(`URL parameter was not formatted correctly: listGroupMembershipsRequest.parent.`)
    end
    
    local url = string.format("%s%s/cloud/v2/%s/memberships", getApisUrl(), getUrlPrefix(headers), tostring(listGroupMembershipsRequest.parent))
    
    if listGroupMembershipsRequest.maxPageSize == nil and listGroupMembershipsRequest.pageToken == nil and listGroupMembershipsRequest.filter == nil then
        return url
    end
    
    local queryParams = {}
    if listGroupMembershipsRequest.maxPageSize ~= nil then
        table.insert(queryParams, string.format("maxPageSize=%s", tostring(listGroupMembershipsRequest.maxPageSize)))
    end
    if listGroupMembershipsRequest.pageToken ~= nil then
        table.insert(queryParams, string.format("pageToken=%s", tostring(listGroupMembershipsRequest.pageToken)))
    end
    if listGroupMembershipsRequest.filter ~= nil then
        table.insert(queryParams, string.format("filter=%s", tostring(listGroupMembershipsRequest.filter)))
    end
    
    url = string.format("%s?%s", url, table.concat(queryParams, "&"))

    return url
end

function listGroupMemberships(listGroupMembershipsRequest : any, headers : any)
    if listGroupMembershipsRequest == nil then
        return InvalidArgumentError(NIL_REQUEST_ERROR_MESSAGE)
    end
    
    local res = verifyListGroupMembershipsRequest(listGroupMembershipsRequest, `listGroupMembershipsRequest`)
    if res ~= nil then
        return res
    end

    local url = listGroupMembershipsUrl(listGroupMembershipsRequest, headers)
    if typeof(url) ~= STRING_TYPE then
        return url
    end

    return OpenCloudService:HttpRequestAsync({[URL] = url, [REQUEST_TYPE] = "GET", [HEADERS] = headers})
end

OpenCloudService:RegisterOpenCloud("v2", "ListGroupMemberships", listGroupMemberships)

function verifyGetOperationRequest(getOperationRequest, argumentName) : any
    if getOperationRequest == nil then
        return nil
    end
    
    local res = verifyTable(getOperationRequest, argumentName)
    if res ~= nil then
        return res
    end
    
    if getOperationRequest.path == nil then
        return InvalidArgumentError(`Required argument was not provided: {argumentName}.path.`)
    end
    res = verifyString(getOperationRequest.path, `{argumentName}.path`)
    if res ~= nil then
        return res
    end
    
    return nil
end

function getOperationUrl(getOperationRequest : any, headers : any) : string
    if getOperationRequest.path == nil then
        return InvalidArgumentError(`URL parameter provided was nil: getOperationRequest.path.`)
    end
    if string.match(getOperationRequest.path, "^users/([^/]+)/operations/([^/]+)$") == nil then
        return InvalidArgumentError(`URL parameter was not formatted correctly: getOperationRequest.path.`)
    end
    
    local url = string.format("%s%s/cloud/v2/%s", getApisUrl(), getUrlPrefix(headers), tostring(getOperationRequest.path))
    
    return url
end

function getOperation(getOperationRequest : any, headers : any)
    if getOperationRequest == nil then
        return InvalidArgumentError(NIL_REQUEST_ERROR_MESSAGE)
    end
    
    local res = verifyGetOperationRequest(getOperationRequest, `getOperationRequest`)
    if res ~= nil then
        return res
    end

    local url = getOperationUrl(getOperationRequest, headers)
    if typeof(url) ~= STRING_TYPE then
        return url
    end

    return OpenCloudService:HttpRequestAsync({[URL] = url, [REQUEST_TYPE] = "GET", [HEADERS] = headers})
end

OpenCloudService:RegisterOpenCloud("v2", "GetOperation", getOperation)

function verifyGenerateUserThumbnailRequest(generateUserThumbnailRequest, argumentName) : any
    if generateUserThumbnailRequest == nil then
        return nil
    end
    
    local res = verifyTable(generateUserThumbnailRequest, argumentName)
    if res ~= nil then
        return res
    end
    
    if generateUserThumbnailRequest.path == nil then
        return InvalidArgumentError(`Required argument was not provided: {argumentName}.path.`)
    end
    res = verifyString(generateUserThumbnailRequest.path, `{argumentName}.path`)
    if res ~= nil then
        return res
    end
    
    res = verifyNumber(generateUserThumbnailRequest.size, `{argumentName}.size`)
    if res ~= nil then
        return res
    end
    
    res = verifyEnum(generateUserThumbnailRequest.format, `{argumentName}.format`)
    if res ~= nil then
        return res
    end
    
    res = verifyEnum(generateUserThumbnailRequest.shape, `{argumentName}.shape`)
    if res ~= nil then
        return res
    end
    
    return nil
end

function generateUserThumbnailUrl(generateUserThumbnailRequest : any, headers : any) : string
    if generateUserThumbnailRequest.path == nil then
        return InvalidArgumentError(`URL parameter provided was nil: generateUserThumbnailRequest.path.`)
    end
    if string.match(generateUserThumbnailRequest.path, "^users/([^/]+)$") == nil then
        return InvalidArgumentError(`URL parameter was not formatted correctly: generateUserThumbnailRequest.path.`)
    end
    
    local url = string.format("%s%s/cloud/v2/%s:generateThumbnail", getApisUrl(), getUrlPrefix(headers), tostring(generateUserThumbnailRequest.path))
    
    if generateUserThumbnailRequest.size == nil and generateUserThumbnailRequest.format == nil and generateUserThumbnailRequest.shape == nil then
        return url
    end
    
    local queryParams = {}
    if generateUserThumbnailRequest.size ~= nil then
        table.insert(queryParams, string.format("size=%s", tostring(generateUserThumbnailRequest.size)))
    end
    if generateUserThumbnailRequest.format ~= nil then
        table.insert(queryParams, string.format("format=%s", tostring(generateUserThumbnailRequest.format)))
    end
    if generateUserThumbnailRequest.shape ~= nil then
        table.insert(queryParams, string.format("shape=%s", tostring(generateUserThumbnailRequest.shape)))
    end
    
    url = string.format("%s?%s", url, table.concat(queryParams, "&"))

    return url
end

function generateUserThumbnail(generateUserThumbnailRequest : any, headers : any)
    if generateUserThumbnailRequest == nil then
        return InvalidArgumentError(NIL_REQUEST_ERROR_MESSAGE)
    end
    
    local res = verifyGenerateUserThumbnailRequest(generateUserThumbnailRequest, `generateUserThumbnailRequest`)
    if res ~= nil then
        return res
    end

    local url = generateUserThumbnailUrl(generateUserThumbnailRequest, headers)
    if typeof(url) ~= STRING_TYPE then
        return url
    end

    return OpenCloudService:HttpRequestAsync({[URL] = url, [REQUEST_TYPE] = "GET", [HEADERS] = headers})
end

OpenCloudService:RegisterOpenCloud("v2", "GenerateUserThumbnail", generateUserThumbnail)

OpenCloudService:RegistrationComplete()
