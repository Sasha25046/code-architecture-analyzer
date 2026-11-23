namespace CodeArchitectureAnalyzer.Tests;

/// <summary>Приклади для метрик класів, детекторів Клас-бог і Заздрість до чужих властивостей.</summary>
internal static class ClassSamples
{
    public const string Big = """
public class Helper1 { public int Value; }
public class Helper2 { public int Value; }

public class Big
{
    private int _a;
    private int _b;
    private int _c;

    public int M1() { return _a; }
    public int M2() { return _b; }
    public int M3() { return _c; }
    public void M4(Helper1 h) { h.Value++; }
    public void M5(Helper2 h) { h.Value++; }
}
""";

    public const string Cohesive = """
public class Cohesive
{
    private int _a;
    private int _b;
    public int M1() { return _a + _b; }
    public int M2() { return _a - _b; }
    public int M3() { return _a * _b; }
}
""";

    public const string Stateless = """
public class Stateless
{
    public int M1() { return 1; }
    public int M2() { return 2; }
}
""";

    private const string Account = """
public class Account
{
    public decimal Balance;
    public decimal Rate;
    public decimal Fee;
    public decimal Limit;
    public decimal Bonus;
    public int Id;
}
""";

    public const string Envious = Account + """


public class Report
{
    public decimal Compute(Account a)
    {
        return a.Balance * a.Rate + a.Fee - a.Limit + a.Bonus;
    }
}
""";

    // Чотири звернення до Account проти двох власних: частка 4/6 виконана, кількість 4 < 5.
    public const string BelowMinimumCount = Account + """


public class Order
{
    private decimal _x;
    private decimal _y;

    public decimal Total(Account a)
    {
        return _x + _y + a.Balance + a.Rate + a.Fee + a.Limit;
    }
}
""";

    // П’ять звернень до Account проти шести власних: кількість виконана, частка 5/11 < 0,65.
    public const string OwnMembersDominate = Account + """


public class Mixed
{
    private decimal _p;
    private decimal _q;
    private decimal _r;

    public decimal Mix(Account a)
    {
        return _p + _q + _r + _p + _q + _r + a.Balance + a.Rate + a.Fee + a.Limit + a.Bonus;
    }
}
""";

    public const string NoForeignAccess = """
public class Simple
{
    public int Plain(int x)
    {
        return x + 1;
    }
}
""";

    public const string InheritedMembers = """
public class Account
{
    public decimal Balance;
}

public class Base
{
    protected int Value;
}

public class Derived : Base
{
    public decimal M(Account a)
    {
        return Value + Value + Value + Value + Value + a.Balance;
    }
}
""";

    public const string LibraryAccess = """
public class Account
{
    public decimal Balance;
}

public class Reporter
{
    public string M(Account a)
    {
        return System.Console.ReadLine() + a.Balance.ToString();
    }
}
""";
}
