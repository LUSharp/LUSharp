--!nonstrict
local CorePackages = game:GetService("CorePackages")

local LuaSocialLibrariesDeps = require(CorePackages.Packages.LuaSocialLibrariesDeps)
local Mock = LuaSocialLibrariesDeps.Mock
local ReportAbuseAnalytics = require(script.Parent.ReportAbuseAnalytics)
local MenuContext = ReportAbuseAnalytics.MenuContexts.LegacyMenu

local JestGlobals = require(CorePackages.Packages.Dev.JestGlobals3)
local expect = JestGlobals.expect
local beforeAll = JestGlobals.beforeAll
local it = JestGlobals.it
local describe = JestGlobals.describe

local Analytics = nil
local ReportAbuseAnalyticsObj = nil

do
	beforeAll(function()
		Analytics = {
			EventStream = Mock.MagicMock.new({ name = "EventStream" }),
			Diag = Mock.MagicMock.new({ name = "Diag" }),
		}
		ReportAbuseAnalyticsObj = ReportAbuseAnalytics.new(Analytics.EventStream, Analytics.Diag, MenuContext)
	end)

	it("SHOULD return a valid object", function()
		expect(ReportAbuseAnalyticsObj).never.toBeNil()
	end)

	describe("ReportAbuseAnalytics", function()
		local Predicates = nil
		local ActionName = nil

		beforeAll(function()
			Predicates = {
				isEventStream = function(x)
					return x == Analytics.EventStream
				end,

				isDiag = function(x)
					return x == Analytics.Diag
				end,

				isTestContext = function(x)
					return x == MenuContext
				end,
			}

			Mock.resetMock(Analytics.EventStream)
		end)

		describe("reportFormSubmitted", function()
			local TimeToComplete = nil
			local MethodOfAbuse = nil

			beforeAll(function()
				ActionName = ReportAbuseAnalytics.ActionNames.FormSubmitted
				TimeToComplete = 1
				MethodOfAbuse = "Voice"

				Predicates.isCategory = function(x)
					return type(x) == "string"
				end

				Predicates.isActionName = function(x)
					return x == ActionName
				end
			end)

			it("SHOULD fire Diag stats with the correct time", function()
				local function isTimeToComplete(x)
					return x == TimeToComplete
				end

				ReportAbuseAnalyticsObj:reportFormSubmitted(TimeToComplete, MethodOfAbuse)

				assert(
					Mock.AnyCallMatches.predicates(
						Analytics.Diag.reportStats,
						Predicates.isDiag,
						Predicates.isCategory,
						isTimeToComplete
					)
				)

				assert(
					Mock.AnyCallMatches.predicates(
						Analytics.Diag.reportCounter,
						Predicates.isDiag,
						Predicates.isCategory,
						function(x)
							return x == 1
						end
					)
				)
			end)

			it("SHOULD fire with the correct additional args", function()
				local additionalArgs = {
					MyArg = "TestArg",
				}

				ReportAbuseAnalyticsObj:reportFormSubmitted(TimeToComplete, MethodOfAbuse, additionalArgs)

				local function isAdditionalArgs(x)
					return type(x) == "table"
						and x.timeToComplete == TimeToComplete
						and x.methodOfAbuse == MethodOfAbuse
				end

				assert(
					Mock.AnyCallMatches.predicates(
						Analytics.EventStream.sendEventDeferred,
						Predicates.isEventStream,
						Predicates.isTestContext,
						Predicates.isActionName,
						isAdditionalArgs
					)
				)
			end)
		end)

		describe("reportFormAbandoned", function()
			local TimeToExit = nil

			beforeAll(function()
				ActionName = ReportAbuseAnalytics.ActionNames.FormAbandoned
				TimeToExit = 1

				Predicates.isCategory = function(x)
					return type(x) == "string"
				end

				Predicates.isActionName = function(x)
					return x == ActionName
				end
			end)

			it("SHOULD fire Diag stats with the correct time", function()
				local function isTimeToExit(x)
					return x == TimeToExit
				end

				ReportAbuseAnalyticsObj:reportFormAbandoned(TimeToExit)

				assert(
					Mock.AnyCallMatches.predicates(
						Analytics.Diag.reportStats,
						Predicates.isDiag,
						Predicates.isCategory,
						isTimeToExit
					)
				)
			end)

			it("SHOULD fire with the correct additional args", function()
				local additionalArgs = {
					MyArg = "TestArg",
				}

				ReportAbuseAnalyticsObj:reportFormAbandoned(TimeToExit, additionalArgs)

				local function isAdditionalArgs(x)
					return type(x) == "table" and x.timeToExit == TimeToExit
				end

				assert(
					Mock.AnyCallMatches.predicates(
						Analytics.EventStream.sendEventDeferred,
						Predicates.isEventStream,
						Predicates.isTestContext,
						Predicates.isActionName,
						isAdditionalArgs
					)
				)
			end)
		end)

		describe("reportAnalyticsFieldChanged", function()
			beforeAll(function()
				ActionName = ReportAbuseAnalytics.ActionNames.FieldChanged
				Predicates.isActionName = function(x)
					return x == ActionName
				end
			end)

			it("SHOULD fire with the correct additional args", function()
				local additionalArgs = {
					MyArg = "TestArg",
				}

				ReportAbuseAnalyticsObj:reportAnalyticsFieldChanged(additionalArgs)

				local function isAdditionalArgs(x)
					return x == additionalArgs
				end

				assert(
					Mock.AnyCallMatches.predicates(
						Analytics.EventStream.sendEventDeferred,
						Predicates.isEventStream,
						Predicates.isTestContext,
						Predicates.isActionName,
						isAdditionalArgs
					)
				)
			end)

			it("SHOULD fire with correctly with no additional args", function()
				ReportAbuseAnalyticsObj:reportAnalyticsFieldChanged()

				local function isAdditionalArgs(x)
					return type(x) == "table"
				end

				assert(
					Mock.AnyCallMatches.predicates(
						Analytics.EventStream.sendEventDeferred,
						Predicates.isEventStream,
						Predicates.isTestContext,
						Predicates.isActionName,
						isAdditionalArgs
					)
				)
			end)
		end)

		describe("reportButtonClick", function()
			beforeAll(function()
				ActionName = ReportAbuseAnalytics.ActionNames.ButtonActivated
				Predicates.isActionName = function(x)
					return x == ActionName
				end
			end)

			it("SHOULD fire with the correct additional args", function()
				local additionalArgs = {
					MyArg = "TestArg",
				}

				ReportAbuseAnalyticsObj:reportButtonClick(additionalArgs)

				local function isAdditionalArgs(x)
					return x == additionalArgs
				end

				assert(
					Mock.AnyCallMatches.predicates(
						Analytics.EventStream.sendEventDeferred,
						Predicates.isEventStream,
						Predicates.isTestContext,
						Predicates.isActionName,
						isAdditionalArgs
					)
				)
			end)

			it("SHOULD fire with correctly with no additional args", function()
				ReportAbuseAnalyticsObj:reportButtonClick()

				local function isAdditionalArgs(x)
					return type(x) == "table"
				end

				assert(
					Mock.AnyCallMatches.predicates(
						Analytics.EventStream.sendEventDeferred,
						Predicates.isEventStream,
						Predicates.isTestContext,
						Predicates.isActionName,
						isAdditionalArgs
					)
				)
			end)
		end)

		describe("reportEventAndIncrement", function()
			beforeAll(function()
				ActionName = "TestActionName"
				Predicates.isActionName = function(x)
					return x == ActionName
				end

				Predicates.isCategory = function(x)
					return type(x) == "string"
				end

				Predicates.didIncrement = function(x)
					return x == 1
				end
			end)

			it("SHOULD increment the Diag counter", function()
				ReportAbuseAnalyticsObj:reportEventAndIncrement(ActionName)

				assert(
					Mock.AnyCallMatches.predicates(
						Analytics.Diag.reportStats,
						Predicates.isDiag,
						Predicates.isCategory,
						Predicates.didIncrement
					)
				)
			end)

			it("SHOULD fire with the correct additional args", function()
				local additionalArgs = {
					MyArg = "TestArg",
				}

				ReportAbuseAnalyticsObj:reportEventAndIncrement(ActionName, additionalArgs)

				local function isAdditionalArgs(x)
					return x == additionalArgs
				end

				assert(
					Mock.AnyCallMatches.predicates(
						Analytics.EventStream.sendEventDeferred,
						Predicates.isEventStream,
						Predicates.isTestContext,
						Predicates.isActionName,
						isAdditionalArgs
					)
				)
			end)

			it("SHOULD fire with correctly with no additional args", function()
				ReportAbuseAnalyticsObj:reportEventAndIncrement(ActionName)

				local function isAdditionalArgs(x)
					return type(x) == "table"
				end

				assert(
					Mock.AnyCallMatches.predicates(
						Analytics.EventStream.sendEventDeferred,
						Predicates.isEventStream,
						Predicates.isTestContext,
						Predicates.isActionName,
						isAdditionalArgs
					)
				)
			end)
		end)
	end)
end
