namespace FsCrawler

open System

type Queue<'a>(xs : 'a list, rxs : 'a list) =
    new() = Queue([], [])
    static member Empty() = new Queue<'a>([], [])

    member q.IsEmpty = (List.isEmpty xs) && (List.isEmpty rxs)
    member q.Enqueue(x) = Queue(xs, x::rxs)
    member q.TryTake() =
        if q.IsEmpty then None, q
        else
            match xs with
            | [] -> (Queue(List.rev rxs,[])).TryTake()
            | y::ys -> Some(y), (Queue(ys, rxs))

type Url = Url of string

type ContentHash = ContentHash of byte[]

type CrawlSession = CrawlSession of Guid

type Crawl =
    {
        Session : CrawlSession
        Url : Url
        Headers : (string * string) list
        Content : byte[]
    }

type Config =
    {
        StoreCrawl : Crawl -> Async<unit>
        GetNextPages : Crawl -> Url list
        DegreeOfParallelism : int
    }

module Client =
    type Dependencies =
        {
            Fetch : Url -> Async<Crawl>
            Config : Config
        }

    type WorkerMessage =
        | Fetch of Url

    type SupervisorMessage =
        | Start of MailboxProcessor<SupervisorMessage> * Url * AsyncReplyChannel<Url Set>
        | FetchCompleted of Url * (Url list)

    type SupervisorProgress =
        {
            Supervisor : MailboxProcessor<SupervisorMessage>
            ReplyChannel : AsyncReplyChannel<Url Set>
            Workers : MailboxProcessor<WorkerMessage> list
            PendingUrls : Url Queue
            Completed : Url Set
            Dispatched : int
        }

    type SupervisorStatus =
        | NotStarted
        | Running of SupervisorProgress
        | Finished

    let startWorker dependencies (supervisor : MailboxProcessor<SupervisorMessage>) =
        MailboxProcessor.Start(fun inbox ->
            let rec loop () =
                async {
                    let! Fetch(url) = inbox.Receive()
                    let! crawl = dependencies.Fetch url
                    do! dependencies.Config.StoreCrawl(crawl)
                    let nextUrls = dependencies.Config.GetNextPages(crawl)
                    supervisor.Post(FetchCompleted(url, nextUrls))
                    return! loop()
                }
            loop())

    let rec dispatch dependencies progress =
        match progress.PendingUrls.TryTake() with
        | None, _ -> progress
        | Some url, queue ->
            match progress.Workers |> List.tryFind (fun worker -> worker.CurrentQueueLength = 0) with
            | Some idleWorker ->
                idleWorker.Post(Fetch url)
                dispatch dependencies { progress with
                                            PendingUrls = queue
                                            Dispatched = progress.Dispatched + 1 }
            | None when progress.Workers.Length < dependencies.Config.DegreeOfParallelism ->
                let newWorker = startWorker dependencies (progress.Supervisor)
                dispatch dependencies { progress with Workers = newWorker :: progress.Workers }
            | _ ->
                progress

    let enqueueUrls urls progress =
        let pending = progress.PendingUrls |> List.foldBack(fun url pending -> pending.Enqueue(url)) urls
        { progress with PendingUrls = pending }

    let complete url progress =
        { progress with
            Completed = progress.Completed.Add(url)
            Dispatched = progress.Dispatched - 1 }

    let start supervisor replyChannel =
        {
            Supervisor = supervisor
            ReplyChannel = replyChannel
            Workers = []
            PendingUrls = Queue.Empty()
            Completed = Set.empty
            Dispatched = 0
        }

    let handleStart dependencies supervisor url replyChannel =
        start supervisor replyChannel
        |> enqueueUrls [url]
        |> dispatch dependencies
        |> Running

    let handleFetchCompleted dependencies url nextUrls progress =
        let progress =
            progress
            |> complete url
            |> enqueueUrls nextUrls
            |> dispatch dependencies
        if progress.PendingUrls.IsEmpty && progress.Dispatched = 0 then
            progress.ReplyChannel.Reply(progress.Completed)
            Finished
        else
            Running progress

    let handleSupervisorMessage dependencies message state =
        match message with
        | Start (supervisor, url, replyChannel) ->
            match state with
            | NotStarted ->
                handleStart dependencies supervisor url replyChannel
            | _ -> failwith "Invalid state: Can't be started more than once."
        | FetchCompleted(url, nextUrls) ->
            match state with
            | Running progress ->
                handleFetchCompleted dependencies url nextUrls progress
            | _ -> failwith "Invalid state - can't complete fetch before starting."

    let fetchRecursiveInternal dependencies startUrl =
        let supervisor = MailboxProcessor<SupervisorMessage>.Start(fun inbox ->
            let rec loop state =
                async {
                    let! message = inbox.Receive()
                    match state |> handleSupervisorMessage dependencies message with
                    | Finished -> return ()
                    | newState -> return! loop newState
                }
            loop NotStarted)
        supervisor.PostAndAsyncReply(fun replyChannel -> Start(supervisor, startUrl, replyChannel))

    let fetchRecursive (config : Config) (startUrl : Url) : Async<Url Set> =
        let fetch url = async { return failwith "Not Implemented" }
        let dependencies = { Config = config; Fetch = fetch }
        fetchRecursiveInternal dependencies startUrl
