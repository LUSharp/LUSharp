using System;

namespace LUSharpTests;

// --- Custom exception classes (top-level so they are accessible across tests) ---

public class T13_AppException : Exception
{
    public int Code;

    public T13_AppException(string message, int code) : base(message)
    {
        Code = code;
    }
}

public class T13_DatabaseException : T13_AppException
{
    public T13_DatabaseException(string message) : base(message, 500)
    {
    }
}

public static class T13_Exceptions
{
    private static int _pass = 0;
    private static int _fail = 0;

    private static bool FinallyFlag = false;

    private static void Assert(bool condition, string name)
    {
        if (condition)
        {
            _pass = _pass + 1;
            Console.WriteLine("  PASS: " + name);
        }
        else
        {
            _fail = _fail + 1;
            Console.WriteLine("  FAIL: " + name);
        }
    }

    // --- Helper methods used as throw sources ---

    private static void ThrowBasicException()
    {
        throw new Exception("basic error");
    }

    private static void ThrowWithMessage(string msg)
    {
        throw new Exception(msg);
    }

    private static void ThrowAppException()
    {
        throw new T13_AppException("app error", 42);
    }

    private static void ThrowAndRethrow()
    {
        try
        {
            throw new Exception("original");
        }
        catch (Exception)
        {
            throw;
        }
    }

    private static int CatchAndReturn()
    {
        try
        {
            throw new Exception("dummy");
        }
        catch (Exception)
        {
            return 99;
        }
    }

    private static void TriggerFinally()
    {
        try
        {
            throw new Exception("trigger");
        }
        catch (Exception)
        {
            // caught
        }
        finally
        {
            FinallyFlag = true;
        }
    }

    public static void Run()
    {
        _pass = 0;
        _fail = 0;
        Console.WriteLine("=== T13_Exceptions ===");

        // --- try/catch basic ---
        bool caught = false;
        try
        {
            throw new Exception("test");
        }
        catch (Exception)
        {
            caught = true;
        }
        Assert(caught == true, "try/catch basic: exception is caught");

        // --- catch with variable (ex.Message) ---
        string msg = "";
        try
        {
            throw new Exception("hello exception");
        }
        catch (Exception ex)
        {
            msg = ex.Message;
        }
        Assert(msg == "hello exception", "catch variable: ex.Message is correct");

        // --- throw new Exception with message from helper ---
        string throwMsg = "";
        try
        {
            ThrowWithMessage("custom message");
        }
        catch (Exception ex)
        {
            throwMsg = ex.Message;
        }
        Assert(throwMsg == "custom message", "throw with message: message preserved");

        // --- try/finally (finally runs when no exception thrown) ---
        bool finallyRan = false;
        try
        {
            int x = 1 + 1;
        }
        finally
        {
            finallyRan = true;
        }
        Assert(finallyRan == true, "try/finally: finally runs without exception");

        // --- try/catch/finally combined ---
        FinallyFlag = false;
        TriggerFinally();
        Assert(FinallyFlag == true, "try/catch/finally: finally runs when exception caught");

        // --- finally runs even when exception propagates out ---
        bool finallyWhenThrown = false;
        bool exceptionEscaped = false;
        try
        {
            try
            {
                throw new Exception("inner");
            }
            finally
            {
                finallyWhenThrown = true;
            }
        }
        catch (Exception)
        {
            exceptionEscaped = true;
        }
        Assert(finallyWhenThrown == true, "finally: runs even when exception escapes");
        Assert(exceptionEscaped == true, "finally: exception still propagates after finally");

        // --- re-throw (throw;) ---
        string rethrowMsg = "";
        try
        {
            ThrowAndRethrow();
        }
        catch (Exception ex)
        {
            rethrowMsg = ex.Message;
        }
        Assert(rethrowMsg == "original", "rethrow: original message preserved");

        // --- nested try/catch ---
        int nestResult = 0;
        try
        {
            try
            {
                throw new Exception("inner exception");
            }
            catch (Exception)
            {
                nestResult = 1;
                throw new Exception("outer exception");
            }
        }
        catch (Exception ex)
        {
            if (nestResult == 1)
            {
                nestResult = 2;
            }
            Assert(ex.Message == "outer exception", "nested try/catch: outer catch sees outer exception");
        }
        Assert(nestResult == 2, "nested try/catch: inner catch ran before outer catch");

        // --- custom exception class ---
        T13_AppException caughtApp = null;
        try
        {
            ThrowAppException();
        }
        catch (T13_AppException ex)
        {
            caughtApp = ex;
        }
        Assert(caughtApp != null, "custom exception: caught as T13_AppException");
        Assert(caughtApp.Message == "app error", "custom exception: Message preserved");
        Assert(caughtApp.Code == 42, "custom exception: custom field Code preserved");

        // --- multiple catch blocks: specific type caught first ---
        bool appCaught = false;
        bool baseCaught = false;
        try
        {
            throw new T13_AppException("multi-catch test", 1);
        }
        catch (T13_AppException)
        {
            appCaught = true;
        }
        catch (Exception)
        {
            baseCaught = true;
        }
        Assert(appCaught == true, "multiple catch: specific T13_AppException caught");
        Assert(baseCaught == false, "multiple catch: base Exception NOT caught when specific matches");

        // --- multiple catch blocks: base type caught when specific doesn't match ---
        bool baseCaught2 = false;
        bool appCaught2 = false;
        try
        {
            throw new Exception("base only");
        }
        catch (T13_AppException)
        {
            appCaught2 = true;
        }
        catch (Exception)
        {
            baseCaught2 = true;
        }
        Assert(baseCaught2 == true, "multiple catch: base Exception caught when no specific match");
        Assert(appCaught2 == false, "multiple catch: T13_AppException NOT triggered for base Exception");

        // --- exception propagates from method to caller ---
        string propagatedMsg = "";
        try
        {
            ThrowBasicException();
        }
        catch (Exception ex)
        {
            propagatedMsg = ex.Message;
        }
        Assert(propagatedMsg == "basic error", "propagation: exception from method reaches caller catch");

        // --- catch and return value ---
        int returnedFromCatch = CatchAndReturn();
        Assert(returnedFromCatch == 99, "catch and return: value returned from catch block");

        // --- execution continues normally after try/catch ---
        int afterTryCatch = 0;
        try
        {
            afterTryCatch = 1;
            throw new Exception("transient");
        }
        catch (Exception)
        {
            afterTryCatch = 2;
        }
        afterTryCatch = afterTryCatch + 1;
        Assert(afterTryCatch == 3, "execution continues normally after try/catch");

        // --- no exception: try body runs, catch not entered ---
        int noThrow = 0;
        try
        {
            noThrow = 42;
        }
        catch (Exception)
        {
            noThrow = -1;
        }
        Assert(noThrow == 42, "no exception: try body value used, catch not entered");

        // --- T13_DatabaseException inherits T13_AppException ---
        string dbMsg = "";
        int dbCode = 0;
        try
        {
            throw new T13_DatabaseException("db connection failed");
        }
        catch (T13_AppException ex)
        {
            dbMsg = ex.Message;
            dbCode = ex.Code;
        }
        Assert(dbMsg == "db connection failed", "inherited exception: message via base catch");
        Assert(dbCode == 500, "inherited exception: code set by base constructor");

        Console.WriteLine("T13_Exceptions: " + _pass + " passed, " + _fail + " failed");
    }
}
