# What is it

This is a very simple program to test communication with a barcode scanner connected to your PC via USB.
The barcode scanner must be configured as SNAPI for this to work. To learn how to do this, please refer to your barcode scanner documentation, they usually provide barcodes that you can scan and it will automatically configure your scanner.

# How to use it

First, you need to install the Zebra Scanner SDK in your computer. You can do that by going to the following link: https://www.zebra.com/us/en/software/scanner-software/scanner-drivers-and-utilities.html

And then:

1 - Select your operating system.

2 - Click "Developer Portal" to be redirected to the download page.

![image](https://github.com/user-attachments/assets/5a22fc9e-2621-4f9c-af89-9218fa92577b)
![image](https://github.com/user-attachments/assets/f1fbe6ef-dea5-439f-8fdd-0ac3c99e37ca)

After that, open the solution in your preferred IDE, go to Program.cs and replace that serial number by the serial number of your barcode scanner:

![image](https://github.com/user-attachments/assets/95fb85f1-c130-49c8-822f-69fcff9fef29)

When you run the program, you should see some logs telling you if connection was successful, hear 2 beeps (those are test beeps, you can remove them in code if you find them annoying) and it will automatically try to scan a barcode, so keep a barcode in front of your barcode scanner for best results.
![image](https://github.com/user-attachments/assets/baeddae2-c23e-482d-be56-90cfaa584e6e)


