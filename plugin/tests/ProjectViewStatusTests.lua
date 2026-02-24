local ProjectViewStatus = require("../src/ProjectViewStatus")

local function run(describe, it, expect)
    describe("ProjectViewStatus", function()
        it("returns error icon when errors are present", function()
            local image = ProjectViewStatus.getStatusIconImage({ errors = 2, ok = false })
            expect(image):toBe(ProjectViewStatus.ERROR_ICON)
        end)

        it("returns ok icon when explicit no-error count is present", function()
            local image = ProjectViewStatus.getStatusIconImage({ errors = 0, ok = false })
            expect(image):toBe(ProjectViewStatus.OK_ICON)
        end)

        it("returns ok icon when status is explicitly ok", function()
            local image = ProjectViewStatus.getStatusIconImage({ ok = true })
            expect(image):toBe(ProjectViewStatus.OK_ICON)
        end)

        it("returns nil for unknown status", function()
            expect(ProjectViewStatus.getStatusIconImage(nil)):toBeNil()
            expect(ProjectViewStatus.getStatusIconImage({})):toBeNil()
            expect(ProjectViewStatus.getStatusIconImage({ dirty = true })):toBeNil()
            expect(ProjectViewStatus.getStatusIconImage({ ok = false })):toBeNil()
        end)

        it("prioritizes errors over ok flags", function()
            local image = ProjectViewStatus.getStatusIconImage({ errors = 1, ok = true })
            expect(image):toBe(ProjectViewStatus.ERROR_ICON)
        end)
    end)
end

return run
