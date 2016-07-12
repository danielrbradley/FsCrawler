let testWhenAnAgentTakesAMessage () =
    let agent = MailboxProcessor.Start(fun inbox ->
        let rec loop () =
            async {
                do! Async.Sleep 1000
                printfn "Taking message"
                let! message = inbox.Receive()
                printfn "Message taken"
                do! Async.Sleep 1000
                printfn "Finishing message"
                return! loop()
            }
        loop())
    printfn "Queue count %i" agent.CurrentQueueLength
    printfn "Queing message"
    agent.Post()
    for _ in 0..20 do
        printfn "Queue count %i" agent.CurrentQueueLength
        System.Threading.Thread.Sleep 100
