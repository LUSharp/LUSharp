local function rescaleCFramePosition(cframe: CFrame, scalingFactor: number): CFrame
	return cframe.Rotation + cframe.Position * scalingFactor
end

return {
	rescaleCFramePosition = rescaleCFramePosition,
}
