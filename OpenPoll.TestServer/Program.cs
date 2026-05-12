using System;
using System.Threading;
using OpenSlave.Services;

const int Port = 1502;

using var slave = new ModbusTcpSlave();

Console.WriteLine($"Modbus TCP test server listening on 0.0.0.0:{Port}");
Console.WriteLine("Holding registers 0..9 = 100,200,...,1000 (counting up each second)");
Console.WriteLine("Coils 0,2,4,... = ON; 1,3,5,... = OFF");
Console.WriteLine("Discrete inputs follow a sweep pattern");
Console.WriteLine("Input registers 0..9 = negative test values");
Console.WriteLine();
Console.WriteLine("Configure OpenPoll: Connection Setup -> TCP, 127.0.0.1:1502, Node ID 1");
Console.WriteLine("Press Ctrl+C to stop.");

for (int i = 0; i < 10; i++)
{
    slave.Coils[i] = (i % 2 == 0);
    slave.DiscreteInputs[i] = (i % 3 == 0);
    slave.HoldingRegisters[i] = (ushort)((i + 1) * 100);
    slave.InputRegisters[i] = unchecked((ushort)(short)(-1000 + i * 100));
}

slave.Start(Port);

var counter = 0;
var sweepIndex = 0;
using var ctrlC = new ManualResetEventSlim(false);
Console.CancelKeyPress += (_, args) => { args.Cancel = true; ctrlC.Set(); };

while (!ctrlC.IsSet)
{
    counter++;

    for (int i = 0; i < 10; i++)
        slave.HoldingRegisters[i] = (ushort)((i + 1) * 100 + counter);

    sweepIndex = (sweepIndex + 1) % 10;
    for (int i = 0; i < 10; i++)
        slave.DiscreteInputs[i] = (i == sweepIndex);

    if (counter % 5 == 0)
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] tick #{counter}");

    ctrlC.Wait(TimeSpan.FromSeconds(1));
}

slave.Stop();
Console.WriteLine("Server stopped.");
