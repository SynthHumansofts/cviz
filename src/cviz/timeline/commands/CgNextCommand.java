package cviz.timeline.commands;

import cviz.IProcessor;
import se.svt.caspar.amcp.AmcpLayer;

public class CgNextCommand extends ICommand {
    public CgNextCommand(int layerId) {
        super(layerId);
    }

    @Override
    public boolean execute(IProcessor processor) {
        AmcpLayer layer = processor.getLayer(getLayerId());

        try {
            layer.sendCommand("CG", "NEXT 1");
            return true;
        } catch (Exception e){
            System.err.println("Failed to execute command: " + e.getMessage());
            return false;
        }
    }

    @Override
    public String toString() {
        return "CgNextCommand: " + getLayerId();
    }
}
