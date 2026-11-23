namespace CodeArchitectureAnalyzer.Tests;

/// <summary>Приклади для тестів StrategyDetector.</summary>
internal static class StrategySamples
{
    private const string Discounts = """
public interface IDiscount
{
    decimal Apply(decimal price);
}

public class NoDiscount : IDiscount
{
    public decimal Apply(decimal price) => price;
}

public class PercentDiscount : IDiscount
{
    public decimal Apply(decimal price) => price * 0.9m;
}
""";

    // ---------- Позитивні ----------

    public const string ConstructorInjection = Discounts + """


public class Order
{
    private readonly IDiscount _discount;

    public Order(IDiscount discount)
    {
        _discount = discount;
    }

    public decimal Total(decimal price) => _discount.Apply(price);
}
""";

    public const string NullChecksAndConditionalAccess = Discounts + """


public class SafeOrder
{
    private readonly IDiscount _discount;

    public SafeOrder(IDiscount discount)
    {
        this._discount = discount ?? throw new System.ArgumentNullException(nameof(discount));
    }

    public decimal Total(decimal price) => this._discount?.Apply(price) ?? price;
}
""";

    public const string AbstractClassWithSetter = """
public abstract class SortStrategy
{
    public abstract void Sort(int[] data);
}

public class BubbleSort : SortStrategy
{
    public override void Sort(int[] data) { }
}

public class QuickSort : SortStrategy
{
    public override void Sort(int[] data) { }
}

public class Sorter
{
    private SortStrategy _strategy;

    public void SetStrategy(SortStrategy strategy)
    {
        _strategy = strategy;
    }

    public void Run(int[] data)
    {
        _strategy.Sort(data);
    }
}
""";

    // ---------- Негативні ----------

    public const string SingleImplementation = """
public interface IRepository
{
    void Save(string item);
}

public class SqlRepository : IRepository
{
    public void Save(string item) { }
}

public class Service
{
    private readonly IRepository _repository;

    public Service(IRepository repository)
    {
        _repository = repository;
    }

    public void Add(string item) => _repository.Save(item);
}
""";

    public const string Decorator = """
public interface IText
{
    string Render();
}

public class Plain : IText
{
    public string Render() => "plain";
}

public class Bold : IText
{
    public string Render() => "bold";
}

public class Decorator : IText
{
    private readonly IText _inner;

    public Decorator(IText inner)
    {
        _inner = inner;
    }

    public string Render() => "<b>" + _inner.Render() + "</b>";
}
""";

    public const string NoDelegation = Discounts + """


public class IdleOrder
{
    private readonly IDiscount _discount;

    public IdleOrder(IDiscount discount)
    {
        _discount = discount;
    }

    public decimal Total(decimal price) => price;
}
""";

    public const string NoInjection = Discounts + """


public class FixedOrder
{
    private readonly IDiscount _discount = new NoDiscount();

    public decimal Total(decimal price) => _discount.Apply(price);
}
""";

    public const string PublicField = Discounts + """


public class OpenOrder
{
    public IDiscount Discount;

    public decimal Total(decimal price) => Discount.Apply(price);
}
""";

    public const string LibraryContractOnly = """
public class Holder
{
    private readonly System.IDisposable _resource;

    public Holder(System.IDisposable resource)
    {
        _resource = resource;
    }

    public void Close() => _resource.Dispose();
}
""";
}
