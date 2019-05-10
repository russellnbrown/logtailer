import sys
import os
import logging
import time
import random


log = logging.getLogger(__name__)

handler = logging.StreamHandler()
handler.formatter = logging.Formatter('%(levelname)s : %(message)s')
handler.level = logging.DEBUG
fhandler = logging.FileHandler('ptest.log','w+')
fhandler.level = logging.DEBUG
fhandler.formatter = logging.Formatter('%(asctime)s %(levelname)s : %(message)s')
log.setLevel(logging.DEBUG)
log.addHandler(handler)
log.addHandler(fhandler)

animals = [ "aardvark", "baboon", "cat", "dog", "emu", "fish", "goat", "horse",
            "iquana", "jackdaw", "kite", "lizard", "manitoo", "natterjack toad", "ocelot" ]

def procsetdir():
        rl = random.randint(1,10)
        ra = random.randint(0,len(animals)-1)
        animal = animals[ra]
        if rl == 1:
                log.error("Hello, this is an error message from the %s", animal)
        elif rl == 2:
                log.warn("Hello, this is a warning message from the %s", animal)
        elif rl == 2 or rl == 3:
                log.info("Hello, this is an info message from the %s", animal)
        else:
                log.debug("Hello, this is a debug message from the test %s", animal)

random.seed()
delay = 0.5
if len(sys.argv) == 2:
        delay = float(sys.argv[1])
while 1 < 2:
        procsetdir()
        time.sleep(delay)
