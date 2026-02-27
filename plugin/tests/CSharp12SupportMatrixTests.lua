local Matrix = require("../src/CSharp12SupportMatrix")

local function run(describe, it, expect)
    describe("CSharp12SupportMatrix", function()
        it("exposes csharp12 feature inventory keys", function()
            expect(type(Matrix)):toBe("table")
            expect(type(Matrix.primary_constructors)):toBe("table")
            expect(type(Matrix.collection_expressions)):toBe("table")
            expect(type(Matrix.lambda_default_parameters)):toBe("table")
            expect(type(Matrix.ref_readonly_parameters)):toBe("table")
            expect(type(Matrix.alias_any_type)):toBe("table")
            expect(type(Matrix.inline_arrays)):toBe("table")
            expect(type(Matrix.experimental_attribute)):toBe("table")
            expect(type(Matrix.interceptors)):toBe("table")
        end)

        it("defines parse/intellisense/compile flags for each feature", function()
            for featureName, feature in pairs(Matrix) do
                expect(type(featureName)):toBe("string")
                expect(type(feature.parse)):toBe("boolean")
                expect(type(feature.intellisense)):toBe("boolean")
                expect(type(feature.compile)):toBe("boolean")
            end
        end)

        it("marks interceptors as explicit unsupported compile path", function()
            expect(Matrix.interceptors.compile):toBe(false)
            expect(type(Matrix.interceptors.diagnosticCode)):toBe("string")
            expect(Matrix.interceptors.diagnosticCode):toContain("interceptors")
        end)
    end)
end

return run
