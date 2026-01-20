local WIDTH_TO_HEIGHT_RATIO = 188 / 285
local SMALL_TO_LARGE_RATIO = 130 / 285
local MAX_HEIGHT = 285
local MAX_RATIO = 0.7

local function getWidth(height: number): number
	return height * WIDTH_TO_HEIGHT_RATIO
end

local function getSize(screenSize: Vector2, large: boolean): Vector2
	local height: number = math.min(math.round(screenSize.Y * MAX_RATIO), MAX_HEIGHT)
	local width: number = getWidth(height)

	if not large then
		height *= SMALL_TO_LARGE_RATIO
	end

	return Vector2.new(width, height)
end

return {
	getSize = getSize,
}
