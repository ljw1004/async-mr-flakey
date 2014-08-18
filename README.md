# Mr Flakey -- helps you write robust async code

It's a fact of life that network operations might fail. I wanted to check that my client apps behave correctly even if calls to REST services fail. So here's what I do:

```vb
Dim r = Await http.GetStringAsync(uri).Flakey()
```

I wrote this extension method called "Flakey()". I call it on every single async call in my code that might fail (and whose failure I want to test). Mr Flakey puts up a bar across the top of the screen. As I run and test my app, I can interactively simulate failure of any async call.

* Download Mr Flakey source code and example project from github [both VB and C#, for Windows8.1 and Phone8.1, requires VS2013]
* Read on below for background, explanation, and testing philosophy.

![screenshot](http://blogs.msdn.com/cfs-filesystemfile.ashx/__key/communityserver-blogs-components-weblogfiles/00-00-01-12-06/7888.async_2D00_mr_2D00_flakey.png)


## Network Failure Is A Fact Of Life

The First Law of Distributed Systems says that a network operation might end in three ways:

1. They might succeed and you know it (e.g. 200 OK)
2. They might fail and you know it (e.g. 500 Failure)
3. Or they might succeed/fail but you don't know which (e.g. Timeout or ConnectionClosed)

These three ways are a fact of life, and your code has to handle all three possibilities. (If you use a network library that doesn't recognize and expose the three possibilities then you'll be unable to write correct code with it.)

My client apps make calls to REST services. Sometimes they do quite complicated sequences of calls -- e.g. fetch an index of records from the cloud, figure out which local records on disk need to be uploaded, attempt the upload, and handle conflicts by auto-merging and re-submitting in a loop. This sequence of calls and the logic between them is basically a distributed algorithm. So what things do I need to check?

## Things to test about async network calls
* Does each individual REST call in my code handle all failure modes correctly?
* Does my *distributed algorithm as a whole* (i.e. the sequence of calls, the re-submit logic) recover correctly if any step along the way has any of the failure modes?
* If a POST/PUT/DELETE operation succeeded on the server but I nevertheless got Timeout or ConnectionClosed or my app was shut down before I could record the successful result -- will my client app still recover correctly when I retry the operation?
* When my client app is shut-down and then restarted, does it recover correctly?
* How do all three modes work when the online resource is in various states? How do they work if another client modifies the online resource in the meantime?

The test matrix explodes exponentially. There's no way you can *comprehensively* test the correctness of your distributed algorithm.

## Unit tests, vs ad-hoc testing

**Unit tests**. In my experience unit tests aren't very good for distributed algorithms. They take a fair bit of effort to set up (e.g. to mock the network), and they're doomed to not be comprehensive enough, and in my experience they *just aren't very good* at finding distributed bugs. I suspect it's because coders' brains think mostly along the "success" path of their algorithm, and aren't good at envisaging all the failure paths.

(Think: when you look at concurrent code, are you the person who spots race conditions just by looking at it? Can anyone on your team do this? Have you ever written a unit test that uncovered a race condition? I haven't.)

**Ad-hoc testing**. But what's great at finding bugs, in my experience, is *interactive ad-hoc testing*. If the UI to simulate failure is easy enough, you can almost set your mind on auto-pilot and randomly click here and there, and you quickly find places where the app doesn't behave right.

I was struck by Leslie Lamport's work on the "Temporal Logic Checker" in the 1990s -- a way to automatically explore that huge exponential space of possibilities (similar to what we face in distributed algorithms). What he found was that "automated random walks" through the space of possibilities *ended up in practice finding just about all the bugs*, and was vastly more efficient than an exhaustive test of every single possibility.

## Introducing Mr Flakey

Mr Flakey helps with ad-hoc random testing of Windows8.1/Phone8.1 async code.

First, call `MrFlakey.Start()` somewhere in your code, e.g. the constructor of your main page. In VB I called it `StartMrFlakey()`.

Whenever you call `Dim task = FooAsync().Flakey()` then a few things happen:
* The *underlying task* that was returned by FooAsync gets *gated* by Mr Flakey.
* At any time, you can click the FAIL button to return a failed task, instead of the underlying task.
* Or, once the underlying task has finished, you can click OK to return its result.

## Diminishing returns

In practice, Mr Flakey seems to find bugs and is easy to use. That's a good balance.

Be sure to use him also for library calls you make which (you assume) *use network stuff under the hood*. For instance, `LiveAuthClient.LoginAsync()` looks like a reliable method provided by Microsoft so you might think you don't need to test its failure. But under the hood, it communicates over the network, so you *should* do `LiveAuthClient.LoginAsync().Flakey()` to test how your code will respond when the network fails.

 

## There are a few enhancements that didn't seem worth the effort...

First, note that Mr Flakey never modifies the underlying task. When you do `Await DoPUTAsync().Flakey()`, then Mr Flakey will never prevent the PUT request from reaching the webservice, and has no way to prevent the webservice from fulfilling that request. All Mr Flakey can simulate is the (more difficult) failure mode where your code thinks the call failed but really it succeeded. (If you wanted to write a Strict Mr Flakey who could prevent the call from going through to the website, you'd have to write `Await StrictMrFlakey(Function() FooAsync())`, which isn't as much fun...)

Second, I only put Mr Flakey on async calls that return Task or Task<T>. All my network calls are asynchronous, so there didn't seem any point in making Mr Flakey also work on synchronous calls (and I wouldn't even know how to do that).

Third, in practice, I just didn't bother using Mr Flakey for StorageFile APIs. I figured I can probably trust that file-system calls will succeed.

 

 

I hope Mr Flakey helps you write robust async network code, as much as he's helped me!
