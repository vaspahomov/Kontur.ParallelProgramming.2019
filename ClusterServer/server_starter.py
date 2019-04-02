import os
import random
'''ClusterServer.exe -p 8061 -n qqq -d 3000 -a'''
port = 8061
word = 'qqq'
delay = 1000
import threading
threads = []
for e in range(8060, 8080):
    threads.append(
        threading.Thread(
            target=os.system,
            args=(f'ClusterServer.exe -p {e} -n {word} -d {random.randint(100,1000)} -a',)))

for e in threads:
    e.start()