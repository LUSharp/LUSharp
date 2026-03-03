namespace RoslynLuau;

public class LanguageFeaturesTest
{
    public static string SafeGetText(object obj)
    {
        try
        {
            return obj.ToString();
        }
        catch
        {
            return "error";
        }
    }

    public static string SafeGetTextWithVar(object obj)
    {
        try
        {
            return obj.ToString();
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }

    public static string SafeGetTextWithFinally(object obj)
    {
        string result = "";
        try
        {
            result = obj.ToString();
        }
        catch (Exception e)
        {
            result = e.Message;
        }
        finally
        {
            Console.WriteLine("done");
        }
        return result;
    }

    public static int SumWithSkip(int[] numbers, int skipValue)
    {
        int sum = 0;
        foreach (var n in numbers)
        {
            if (n == skipValue)
                continue;
            sum += n;
        }
        return sum;
    }

    public static int ApplyTransform(int value, int multiplier)
    {
        // Note: since we can't easily use Func<T> generics yet,
        // we'll test lambda as a local usage pattern
        var result = value * multiplier;
        return result;
    }

    public static string GetOrDefault(string value, string fallback)
    {
        return value ?? fallback;
    }

    public static int CountDown(int start)
    {
        int count = 0;
        int i = start;
        do
        {
            count++;
            i--;
        } while (i > 0);
        return count;
    }
}
