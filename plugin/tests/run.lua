-- LUSharp Test Runner
-- Run with: luau plugin/tests/run.lua

local passed = 0
local failed = 0
local errors = {}

local function describe(name, fn)
    print("\n" .. name)
    fn()
end

local function it(name, fn)
    local ok, err = pcall(fn)
    if ok then
        passed += 1
        print("  PASS  " .. name)
    else
        failed += 1
        table.insert(errors, { name = name, err = err })
        print("  FAIL  " .. name)
        print("        " .. tostring(err))
    end
end

local function expect(value)
    return {
        toBe = function(_, expected)
            if value ~= expected then
                error("Expected " .. tostring(expected) .. ", got " .. tostring(value), 2)
            end
        end,
        toEqual = function(_, expected)
            local function deepEqual(a, b)
                if type(a) ~= type(b) then return false end
                if type(a) ~= "table" then return a == b end
                for k, v in pairs(a) do
                    if not deepEqual(v, b[k]) then return false end
                end
                for k, v in pairs(b) do
                    if a[k] == nil then return false end
                end
                return true
            end
            if not deepEqual(value, expected) then
                error("Tables not equal", 2)
            end
        end,
        toContain = function(_, substring)
            if type(value) ~= "string" or not string.find(value, substring, 1, true) then
                error("Expected string to contain '" .. tostring(substring) .. "'", 2)
            end
        end,
        toBeNil = function(_)
            if value ~= nil then
                error("Expected nil, got " .. tostring(value), 2)
            end
        end,
        toNotBeNil = function(_)
            if value == nil then
                error("Expected non-nil value", 2)
            end
        end,
        toHaveLength = function(_, len)
            if #value ~= len then
                error("Expected length " .. len .. ", got " .. #value, 2)
            end
        end,
    }
end

-- Load and run test files
-- Each test file returns a function(describe, it, expect)
local testFiles = {
    "LexerTests",
    "ParserTests",
    "LowererTests",
    "EmitterTests",
}

for _, name in ipairs(testFiles) do
    local ok, err = pcall(function()
        local testModule = require("./" .. name)
        if type(testModule) == "function" then
            testModule(describe, it, expect)
        end
    end)
    if not ok then
        print("ERROR loading " .. name .. ": " .. tostring(err))
    end
end

print("\n---")
print(passed .. " passed, " .. failed .. " failed")
if #errors > 0 then
    print("\nFailures:")
    for _, e in ipairs(errors) do
        print("  " .. e.name .. ": " .. e.err)
    end
end
