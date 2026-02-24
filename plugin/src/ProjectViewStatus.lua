local ProjectViewStatus = {}

ProjectViewStatus.ERROR_ICON = "rbxassetid://110766733373274"
ProjectViewStatus.OK_ICON = "rbxassetid://80623917691369"

function ProjectViewStatus.getStatusIconImage(status)
    if type(status) ~= "table" then
        return nil
    end

    local errors = status.errors
    if type(errors) == "number" then
        if errors > 0 then
            return ProjectViewStatus.ERROR_ICON
        end

        return ProjectViewStatus.OK_ICON
    end

    if status.ok == true then
        return ProjectViewStatus.OK_ICON
    end

    return nil
end

return ProjectViewStatus
