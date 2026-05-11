using System;
using System.Threading;
using EasyModbus;

const int Port = 1502;

var server = new ModbusServer { Port = Port };
server.Listen();

Console.WriteLine($"Modbus TCP test server listening on 0.0.0.0:{Port}");
Console.WriteLine("Holding registers 1..10 = 100,200,...,1000 (counting up each second)");
Console.WriteLine("Coils 1,3,5,... = ON; 2,4,6,... = OFF");
Console.WriteLine("Discrete inputs follow a sweep pattern");
Console.WriteLine("Input registers 1..10 = negative test values");
Console.WriteLine();
Console.WriteLine("Configure the scanner: Connection Setup -> TCP, 127.0.0.1:1502, Node ID 1");
Console.WriteLine("Press Ctrl+C to stop.");

for (int i = 0; i < 10; i++)
{
    server.coils[i + 1] = (i % 2 == 0);
    server.discreteInputs[i + 1] = (i % 3 == 0);
    server.holdingRegisters[i + 1] = (short)((i + 1) * 100);
    server.inputRegisters[i + 1] = (short)(-1000 + i * 100);
}

var counter = 0;
var sweepIndex = 0;
using var ctrlC = new ManualResetEventSlim(false);
Console.CancelKeyPress += (_, args) => { args.Cancel = true; ctrlC.Set(); };

while (!ctrlC.IsSet)
{
    counter++;

    for (int i = 0; i < 10; i++)
        server.holdingRegisters[i + 1] = (short)((i + 1) * 100 + counter);

    sweepIndex = (sweepIndex + 1) % 10;
    for (int i = 0; i < 10; i++)
        server.discreteInputs[i + 1] = (i == sweepIndex);

    if (counter % 5 == 0)
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] tick #{counter}");

    ctrlC.Wait(TimeSpan.FromSeconds(1));
}

server.StopListening();
Console.WriteLine("Server stopped.");
