//try catch 可以跳过一些不确定的代码 以 保证后续的代码正常执行  catch也可以捕捉c#的异常
function trycatch(arg,th)
{
    try
    {
        if (arg) {
            throw th
        }
        print(arg + "     hello world   ")
    }
    catch(e)
    {
        print("error : " + th)
    }
    print("finished")
}
trycatch(false)
trycatch(true, "ffffffffffffffff")