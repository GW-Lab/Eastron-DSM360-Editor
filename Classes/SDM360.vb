﻿' Program..: SDM360.vb
' Author...: G. Wassink
' Design...: 
' Date.....: 10/04/2019 Last revised:10/04/2019
' Notice...: Copyright 2019 All Rights Reserved
' Notes....: VB 16.0.1 .Net Framework 4.7.2
' Files....: None
' Programs.:
' Reserved.: 

Imports System.IO.Ports

Public Class SDM360 : Inherits SerialPort
   Const ModBusReadPacketLengthExclPayload = 7
   Const ModBusWritePacketLengthExclPayload = 6

   Private currModBusID As UShort
   Private currFun As Byte
   Private currUnit As Unit
   Private currStartAddress As UShort
   Private currQuantity As UShort
   Private bytesExpected As Integer = 0
   Private requestSend As Boolean = False

   Private ReadOnly tmrTimeOut As New Threading.Timer(AddressOf Time_Out_Tick, Nothing, dueTime:=InfiniteTimeout, period:=InfiniteTimeout)
   Private ReadOnly tmrReadInterval As New Threading.Timer(AddressOf Read_Interval_Tick, Nothing, dueTime:=InfiniteTimeout, period:=InfiniteTimeout)

   Public Event Receive_Data_Changed(value As Single, unit As Unit, fun As ModBusFun, message As String)
   Public Event Device_Status_Changed(status As Boolean, Message As String)

   Private Sub Read_Interval_Tick(obj As Object)
      ReadRegisters(Me.currModBusID, Me.currFun, Me.currStartAddress, Me.currquantity)
   End Sub

   Public WriteOnly Property ReadTimer As Boolean
      Set(value As Boolean)
         If value Then
            Me.tmrReadInterval.Change(0, My.Settings.ReadInterval * 1000)
         Else
            Me.tmrReadInterval.Change(dueTime:=InfiniteTimeout, period:=InfiniteTimeout)
         End If
      End Set
   End Property

   Private Sub Time_Out_Tick(obj As Object)
      StopTimeOut()

      If BytesToRead = 5 Then
         RaiseEvent Device_Status_Changed(False, "Value out of range")
      Else
         RaiseEvent Device_Status_Changed(False, $"ModBus: {Me.currModBusID} Timeout")
      End If

      DiscardOutBuffer()
      DiscardInBuffer()
   End Sub

   Private Sub StartTimeOut() ' Start TimeOut-Timer
      Me.requestSend = True
      Me.tmrTimeOut.Change(1500, 100000)
   End Sub

   Private Sub StopTimeOut() ' Stop TimeOut-Timer
      Me.tmrTimeOut.Change(dueTime:=InfiniteTimeout, period:=InfiniteTimeout)
      Me.requestSend = False
   End Sub

   Public Enum Unit
      Ampere
      BaudRate
      Degrees
      ModBusID
      kWh
      Minutes
      ParityStopBits
      Password
      PasswordLock
      PulsDivisor
      RelayPulsWidth
      SerialNr
      SystemType
      SystemVolts
      VA
      Volt
      Watt
      Unkwown
   End Enum

   Public Enum ModBusFun As Byte
      Holding = 3
      HoldingWrite = 16
      Input = 4
   End Enum

   Public Sub ReadRegisters(modBusId As Byte, fun As ModBusFun, startAddress As UShort, quantity As UShort)
      If IsOpen Then
         If Not Me.requestSend Then
            Dim packet = New ModBusPacket
            packet.AddRange({modBusId, fun})                                                                                        ' DeviceIndentifier/ModBus Fuction-Code 3 = Read Holding register, Fuction Code 4 = Read Input register
            packet.AddRange(RebaseAddress(startAddress, fun))                                                                       ' StartAddress SDM360 internal = 00
            packet.AddRange(BitConverter.GetBytes(quantity * 2US).Reverse)                                                          ' Quantity
            packet.AddCRC()                                                                                                         ' Read ALL data added to the packet until now Compute CRC16 and append the CRC16 to the packet

            Me.bytesExpected = ModBusReadPacketLengthExclPayload + quantity * 2

            Me.currModBusID = modBusId
            Me.currFun = fun
            Me.currUnit = GetUnit(startAddress)
            Me.currStartAddress = startAddress
            Me.currQuantity = quantity

            StartTimeOut()

            Write(packet.ToArray, 0, packet.Count)
         Else
            '  Return True
         End If
      Else
         RaiseEvent Device_Status_Changed(False, PortName + ": not open")
      End If
   End Sub

   Public Sub WriteHoldingRegisters(modBusId As Byte, startAddress As UShort, quantity As UShort, value As Single)
      If IsOpen Then
         If Not Me.requestSend Then
            Dim packet = New ModBusPacket
            packet.AddRange({modBusId, ModBusFun.HoldingWrite})                                                                     ' DeviceIndentifier/ModBus Fuction-Code 3 = Read Holding register, Fuction Code 4 = Read Input register
            packet.AddRange(RebaseAddress(startAddress, ModBusFun.HoldingWrite))                                                    ' StartAddress SDM360 internal = 00
            packet.AddRange(BitConverter.GetBytes(quantity * 2US).Reverse)                                                          ' Number of registers 
            packet.Add(CByte(quantity * 4US))                                                                                       ' Byte count
            packet.AddRange(BitConverter.GetBytes(value).Reverse)                                                                   ' Payload
            packet.AddCRC()                                                                                                         ' Read ALL data added to the packet until now Compute CRC16 and append the CRC16 to the packet

            Me.bytesExpected = ModBusWritePacketLengthExclPayload + quantity * 2
            Me.currUnit = GetUnit(startAddress)
            Me.currModBusID = modBusId
            Me.currFun = ModBusFun.HoldingWrite

            StartTimeOut()

            Write(packet.ToArray, 0, packet.Count)
         End If
      Else
         RaiseEvent Device_Status_Changed(False, PortName + ": not open")
      End If
   End Sub

   Private Function RebaseAddress(Address As UShort, fun As ModBusFun) As IEnumerable(Of Byte)
      Return BitConverter.GetBytes(If(fun = ModBusFun.Input, Address - 30001US, Address - 40001US)).Reverse
   End Function

   Private Function GetUnit(startAddress As UShort) As Unit
      Select Case startAddress
         Case 30001 To 30005 : Return Unit.Volt
         Case 30007 To 30011 : Return Unit.Ampere
         Case 30013 To 30017 : Return Unit.Watt
         Case 30019 To 30023 : Return Unit.VA
         Case 30037 To 30041 : Return Unit.Degrees
         Case 40003 To 40003 : Return Unit.Minutes
         Case 40007 To 40007 : Return Unit.SystemVolts
         Case 40009 To 40009 : Return Unit.Ampere
         Case 40011 To 40011 : Return Unit.SystemType
         Case 40013 To 40013 : Return Unit.RelayPulsWidth
         Case 40015 To 40015 : Return Unit.PasswordLock
         Case 40019 To 40019 : Return Unit.ParityStopBits
         Case 40021 To 40021 : Return Unit.ModBusID
         Case 40023 To 40023 : Return Unit.PulsDivisor
         Case 40025 To 40025 : Return Unit.Password
         Case 40029 To 40036 : Return Unit.BaudRate
         Case 40037 To 40037 : Return Unit.Volt
         Case 40043 To 40043 : Return Unit.SerialNr
         Case Else
            Return Unit.Unkwown
      End Select
   End Function

   Private Sub ModBusBaseSerial_DataReceived(sender As Object, e As SerialDataReceivedEventArgs) Handles Me.DataReceived
      If BytesToRead > 0 AndAlso Me.bytesExpected = BytesToRead Then ' Data to process?
         StopTimeOut()

         Dim buffer = New Byte(BytesToRead - 1) {}                   ' Create receive buffer 
         Read(buffer, 0, buffer.Length)                              ' Read data into receive buffer

         If ModBusPacket.CRC16(buffer) Then
            RaiseEvent Receive_Data_Changed(SDM360Client.ConvertToFloat({buffer(3), buffer(4), buffer(5), buffer(6)}), Me.currUnit, Me.currFun, "Ok")
         Else
            RaiseEvent Receive_Data_Changed(0, Me.currUnit, Me.currFun, "CRC-Error!!")
         End If
      End If
   End Sub
End Class