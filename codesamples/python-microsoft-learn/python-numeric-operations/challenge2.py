print('Simple calculator!')

first_value = input('First number? ')
operator = input('Operation? ')
second_value = input('Second number? ')

if first_value.isnumeric() == False or second_value.isnumeric() == False:
    print('Please input a number.')
    exit()
else:
    first_value = int(first_value)
    second_value = int(second_value)

sum = 0
if operator == '+':
    sum = first_value + second_value
    label = 'Sum'
elif operator == '-':
    sum = first_value - second_value
    label = 'Difference'
elif operator == '*':
    sum = first_value * second_value
    label = 'Product'
elif operator == '/':
    sum = first_value / second_value
    label = 'Quotient'
elif operator == '%':
    sum = first_value % second_value
    label = 'Modulous'
elif operator == '**':
    sum = first_value ** second_value
    label = 'Exponent'
else:
    print('Operation not recognized')
    exit()

print(label + ' of ' + str(first_value) + ' ' + operator + ' ' + str(second_value) + ' equals: ' + str(sum))
