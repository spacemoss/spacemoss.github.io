fahrenheit = input('What is the temperature in Fahrenheit? ')

if fahrenheit.isnumeric() == False:
    print('Input is not a number.')
    exit()

celsius = round(((int(fahrenheit) - 32) * 5 / 9))
print('Temperature in celsius is: ' + str(celsius))