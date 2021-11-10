print('Would you like to continue?')
response = input()
if response == 'no' or response == 'n':
    print('Exiting')
elif response == 'yes' or response == 'y':
    print('Continuing . . .')
    print('Complete!')
else:
    print('Please try again and respond with yes or no.')
