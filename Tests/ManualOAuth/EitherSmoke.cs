using System;
using UnitySample.ManualOAuth;

var checks = 0;
void Check(bool condition)
{
    if (!condition) throw new Exception("Either check failed.");
    checks++;
}
void Reject<TException>(Action action) where TException : Exception
{
    try { action(); }
    catch (TException) { checks++; return; }
    throw new Exception("Expected " + typeof(TException).Name);
}

var callback = new Uri("http://localhost:7890/login-callback?code=a%2Bb");
Either<string, Uri> success = callback;
Check(success.IsRight && !success.IsLeft && ReferenceEquals(success.Right, callback));
Either<string, Uri> error = "Browser unavailable.";
Check(error.IsLeft && !error.IsRight && error.Left == "Browser unavailable.");
Reject<InvalidOperationException>(() => { _ = success.Left; });
Reject<InvalidOperationException>(() => { _ = error.Right; });
Reject<ArgumentNullException>(() => { Either<string, Uri> result = (string)null; });
Reject<ArgumentNullException>(() => { Either<string, Uri> result = (Uri)null; });
Either<int, string> number = 0;
Check(number.IsLeft && number.Left == 0);
Check(Either<string, string>.FromLeft("left").IsLeft && Either<string, string>.FromRight("right").IsRight);
Console.WriteLine($"Passed {checks} Either checks.");
