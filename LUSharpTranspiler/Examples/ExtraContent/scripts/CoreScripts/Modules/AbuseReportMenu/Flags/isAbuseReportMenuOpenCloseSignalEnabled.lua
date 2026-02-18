game:DefineFastFlag("AbuseReportMenuOpenCloseSignal", false)

return function()
	if not game:GetEngineFeature("AbuseReportMenuOpenCloseSignal") then
		return false
	end

	return game:GetFastFlag("AbuseReportMenuOpenCloseSignal")
end
