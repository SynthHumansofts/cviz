package cviz;

import java.net.SocketException;
import java.util.LinkedList;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.CopyOnWriteArrayList;

import cviz.control.CueInterface;
import cviz.timeline.Command;
import cviz.timeline.Parser;
import cviz.timeline.Trigger;
import cviz.timeline.TriggerType;
import lib.ResettableCountDownLatch;
import se.svt.caspar.amcp.*;

public class CasparViz {

	public static void main(String[] args) throws InterruptedException, SocketException {
//		if (args.length < 2){
//			System.out.println("Expected: 127.0.0.1 new.tl");
//			return;
//		}
		
		args = new String[]{
			"127.0.0.1",
			"new.tl"
		};

		System.out.println("Connecting to: " + args[0]);

        System.out.println("Caspar-timeline v0.1 running with timeline: " + args[1]);
        LinkedList<Trigger> triggers = Parser.Parse(args[1]);

		AmcpCasparDevice host = new AmcpCasparDevice(args[0], 5250);
		AmcpChannel channel = new AmcpChannel(host, 1);

        Processor processor = new Processor(channel, triggers);

		(new Thread(new OSC(processor, 5253))).start();
		(new Thread(processor)).start();
		(new Thread(new CueInterface(processor))).start();
	}
}